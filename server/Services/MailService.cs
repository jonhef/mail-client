using System.Text;
using MailClient.Server.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace MailClient.Server.Services;

public sealed class MailService
{
    private readonly AccountStore _accounts;

    public MailService(AccountStore accounts)
    {
        _accounts = accounts;
    }

    private static SecureSocketOptions ToSocketOptions(ServerEndpoint ep)
        => ep.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : (ep.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None);

    private (AccountConfig cfg, AccountSecrets secrets) Load(string accountId)
    {
        var stored = _accounts.GetStored(accountId);
        var secrets = _accounts.GetSecrets(accountId);
        return (stored.Config, secrets);
    }

    private async Task AuthenticateAsync(ImapClient client, AccountConfig cfg, AccountSecrets secrets, CancellationToken ct)
    {
        // oauth пока не делаем по-настоящему, просто пароль
        if (!string.IsNullOrWhiteSpace(secrets.Password))
        {
            await client.AuthenticateAsync(cfg.Email, secrets.Password, ct);
            return;
        }

        throw new InvalidOperationException("no auth method configured");
    }

    private async Task AuthenticateSmtpAsync(SmtpClient client, AccountConfig cfg, AccountSecrets secrets, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(secrets.Password))
        {
            await client.AuthenticateAsync(cfg.Email, secrets.Password, ct);
            return;
        }

        throw new InvalidOperationException("no auth method configured");
    }

    public async Task<FolderDto[]> ListFoldersAsync(string accountId, CancellationToken ct)
    {
        var (cfg, secrets) = Load(accountId);

        using var client = new ImapClient();
        await client.ConnectAsync(cfg.Imap.Host, cfg.Imap.Port, ToSocketOptions(cfg.Imap), ct);
        await AuthenticateAsync(client, cfg, secrets, ct);

        var personal = client.GetFolder(client.PersonalNamespaces[0]);
        var folders = await personal.GetSubfoldersAsync(false, ct);

        // добавим Inbox и корневые специальные
        var all = new List<IMailFolder>();
        all.Add(client.Inbox);
        all.AddRange(folders);

        var result = new List<FolderDto>();
        foreach (var f in all.DistinctBy(x => x.FullName))
        {
            try
            {
                await f.OpenAsync(FolderAccess.ReadOnly, ct);
                var unread = f.Unread;
                var role = f.Attributes.HasFlag(FolderAttributes.Inbox) ? "inbox"
                         : f.Attributes.HasFlag(FolderAttributes.Sent) ? "sent"
                         : f.Attributes.HasFlag(FolderAttributes.Drafts) ? "drafts"
                         : f.Attributes.HasFlag(FolderAttributes.Trash) ? "trash"
                         : f.Attributes.HasFlag(FolderAttributes.Junk) ? "junk"
                         : "folder";

                result.Add(new FolderDto(f.FullName, f.Name, unread, role));
            }
            catch
            {
                // пропускаем проблемные фолдеры
            }
        }

        await client.DisconnectAsync(true, ct);
        return result.OrderByDescending(x => x.Role == "inbox").ThenBy(x => x.Name).ToArray();
    }

    public async Task<ListMessagesResponse> ListMessagesAsync(string accountId, string folderId, string? cursor, int pageSize, CancellationToken ct)
    {
        var (cfg, secrets) = Load(accountId);

        using var client = new ImapClient();
        await client.ConnectAsync(cfg.Imap.Host, cfg.Imap.Port, ToSocketOptions(cfg.Imap), ct);
        await AuthenticateAsync(client, cfg, secrets, ct);

        var folder = await client.GetFolderAsync(folderId, ct);
        await folder.OpenAsync(FolderAccess.ReadOnly, ct);

        // cursor = uidnext-ish: будем листать назад от самого нового uid
        // формат cursor: "uid:<number>" означает следующий верхний uid (эксклюзив) для старых
        uint upperExclusive = uint.MaxValue;
        if (!string.IsNullOrWhiteSpace(cursor) && cursor.StartsWith("uid:", StringComparison.OrdinalIgnoreCase))
        {
            if (uint.TryParse(cursor.Substring(4), out var parsed))
                upperExclusive = parsed;
        }

        var uids = await folder.SearchAsync(SearchQuery.All, ct);
        if (uids.Count == 0)
        {
            await client.DisconnectAsync(true, ct);
            return new ListMessagesResponse(Array.Empty<MessageHeaderDto>(), null);
        }

        // берём последние pageSize uid < upperExclusive
        var filtered = uids.Where(u => u.Id < upperExclusive).ToList();
        filtered.Sort((a, b) => b.Id.CompareTo(a.Id));
        var page = filtered.Take(pageSize).ToList();

        var summaries = await folder.FetchAsync(page, MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate | MessageSummaryItems.BodyStructure | MessageSummaryItems.Size, ct);

        var items = new List<MessageHeaderDto>();
        foreach (var s in summaries)
        {
            var env = s.Envelope;
            var from = env?.From?.Mailboxes?.FirstOrDefault();
            var subject = env?.Subject ?? "";
            var date = (s.InternalDate ?? DateTimeOffset.UtcNow).ToString("o");
            var unread = !(s.Flags?.HasFlag(MessageFlags.Seen) ?? false);
            var hasAtt = s.BodyParts?.Any(p => p.IsAttachment) ?? false;

            // стабильный id: folder + uid
            var id = $"{folderId}::uid::{s.UniqueId.Id}";

            items.Add(new MessageHeaderDto(
                id,
                folderId,
                subject,
                from?.Name ?? "",
                from?.Address ?? "",
                date,
                unread,
                hasAtt,
                s.Size ?? 0
            ));
        }

        // next cursor: min uid on page
        string? next = null;
        if (page.Count == pageSize)
        {
            var minUid = page.Min(u => u.Id);
            next = $"uid:{minUid}";
        }

        await client.DisconnectAsync(true, ct);
        return new ListMessagesResponse(items.ToArray(), next);
    }

    private static UniqueId ParseUidFromMessageId(string messageId, out string folderId)
    {
        // формат: "<folderId>::uid::<uid>"
        var parts = messageId.Split("::uid::", StringSplitOptions.None);
        if (parts.Length != 2) throw new ArgumentException("invalid message id");
        folderId = parts[0];
        if (!uint.TryParse(parts[1], out var uid)) throw new ArgumentException("invalid uid");
        return new UniqueId(uid);
    }

    public async Task<MessageDto> GetMessageAsync(string accountId, string messageId, CancellationToken ct)
    {
        var (cfg, secrets) = Load(accountId);

        using var client = new ImapClient();
        await client.ConnectAsync(cfg.Imap.Host, cfg.Imap.Port, ToSocketOptions(cfg.Imap), ct);
        await AuthenticateAsync(client, cfg, secrets, ct);

        var uid = ParseUidFromMessageId(messageId, out var folderId);
        var folder = await client.GetFolderAsync(folderId, ct);
        await folder.OpenAsync(FolderAccess.ReadOnly, ct);

        var msg = await folder.GetMessageAsync(uid, ct);

        var from = msg.From.Mailboxes.FirstOrDefault();
        var to = msg.To.Mailboxes.Select(m => m.Address).ToArray();
        var cc = msg.Cc.Mailboxes.Select(m => m.Address).ToArray();
        var date = (msg.Date != DateTimeOffset.MinValue ? msg.Date : DateTimeOffset.UtcNow).ToString("o");

        var bodyText = msg.TextBody ?? "";
        var bodyHtml = msg.HtmlBody ?? "";

        var attachments = new List<AttachmentDto>();
        foreach (var a in msg.Attachments)
        {
            if (a is MimePart part)
            {
                var attId = $"{messageId}::att::{attachments.Count}";
                attachments.Add(new AttachmentDto(attId, part.FileName ?? "attachment", part.ContentType.MimeType, part.Content?.Stream?.Length ?? 0));
            }
        }

        // unread: надо summary, но дешево: read-only folder не даёт flags, сделаем fetch
        var sum = (await folder.FetchAsync(new[] { uid }, MessageSummaryItems.Flags, ct)).FirstOrDefault();
        var unread = !(sum?.Flags?.HasFlag(MessageFlags.Seen) ?? false);

        await client.DisconnectAsync(true, ct);

        return new MessageDto(
            messageId,
            folderId,
            msg.Subject ?? "",
            from?.Name ?? "",
            from?.Address ?? "",
            to,
            cc,
            date,
            unread,
            attachments.Count > 0,
            bodyHtml,
            bodyText,
            attachments.ToArray()
        );
    }

    public async Task UpdateMessageAsync(string accountId, string messageId, UpdateMessageRequest req, CancellationToken ct)
    {
        var (cfg, secrets) = Load(accountId);

        using var client = new ImapClient();
        await client.ConnectAsync(cfg.Imap.Host, cfg.Imap.Port, ToSocketOptions(cfg.Imap), ct);
        await AuthenticateAsync(client, cfg, secrets, ct);

        var uid = ParseUidFromMessageId(messageId, out var folderId);
        var folder = await client.GetFolderAsync(folderId, ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);

        if (req.MarkRead == true)
            await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);

        if (req.MarkUnread == true)
            await folder.RemoveFlagsAsync(uid, MessageFlags.Seen, true, ct);

        if (!string.IsNullOrWhiteSpace(req.MoveToFolderId))
        {
            var dest = await client.GetFolderAsync(req.MoveToFolderId, ct);
            await folder.MoveToAsync(uid, dest, ct);
        }

        if (req.Delete == true)
        {
            await folder.AddFlagsAsync(uid, MessageFlags.Deleted, true, ct);
            await folder.ExpungeAsync(ct);
        }

        await client.DisconnectAsync(true, ct);
    }

    public async Task SendEmailAsync(string accountId, SendEmailRequest req, CancellationToken ct)
    {
        var (cfg, secrets) = Load(accountId);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(cfg.DisplayName, cfg.Email));

        foreach (var addr in req.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(MailboxAddress.Parse(addr));

        if (!string.IsNullOrWhiteSpace(req.Cc))
        {
            foreach (var addr in req.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                message.Cc.Add(MailboxAddress.Parse(addr));
        }

        if (!string.IsNullOrWhiteSpace(req.Bcc))
        {
            foreach (var addr in req.Bcc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                message.Bcc.Add(MailboxAddress.Parse(addr));
        }

        message.Subject = req.Subject ?? "";

        var builder = new BodyBuilder
        {
            TextBody = req.BodyText ?? "",
            HtmlBody = req.BodyHtml
        };
        message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(cfg.Smtp.Host, cfg.Smtp.Port, ToSocketOptions(cfg.Smtp), ct);
        await AuthenticateSmtpAsync(smtp, cfg, secrets, ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);

        // опционально: копировать в sent через imap append
        try
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(cfg.Imap.Host, cfg.Imap.Port, ToSocketOptions(cfg.Imap), ct);
            await AuthenticateAsync(imap, cfg, secrets, ct);

            var sent = await TryFindSentFolderAsync(imap, ct) ?? await imap.GetFolderAsync(req.FolderIdSent, ct);
            await sent.OpenAsync(FolderAccess.ReadWrite, ct);
            await sent.AppendAsync(message, MessageFlags.Seen, ct);

            await imap.DisconnectAsync(true, ct);
        }
        catch
        {
            // не валим отправку, если append не удался
        }
    }

    private static async Task<IMailFolder?> TryFindSentFolderAsync(ImapClient client, CancellationToken ct)
    {
        var personal = client.GetFolder(client.PersonalNamespaces[0]);
        var folders = await personal.GetSubfoldersAsync(true, ct);

        var sent = folders.FirstOrDefault(f => f.Attributes.HasFlag(FolderAttributes.Sent));
        return sent;
    }
}
