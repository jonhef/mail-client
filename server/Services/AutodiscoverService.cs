using MailClient.Server.Models;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailClient.Server.Services;

public sealed class AutodiscoverService
{
    private static readonly Dictionary<string, (ServerEndpoint imap, ServerEndpoint smtp)> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gmail.com"] = (
            new ServerEndpoint("imap.gmail.com", 993, true, false),
            new ServerEndpoint("smtp.gmail.com", 587, false, true)
        ),
        ["outlook.com"] = (
            new ServerEndpoint("imap-mail.outlook.com", 993, true, false),
            new ServerEndpoint("smtp-mail.outlook.com", 587, false, true)
        ),
        ["hotmail.com"] = (
            new ServerEndpoint("imap-mail.outlook.com", 993, true, false),
            new ServerEndpoint("smtp-mail.outlook.com", 587, false, true)
        ),
        ["yahoo.com"] = (
            new ServerEndpoint("imap.mail.yahoo.com", 993, true, false),
            new ServerEndpoint("smtp.mail.yahoo.com", 587, false, true)
        )
    };

    private static SecureSocketOptions ToSocketOptions(ServerEndpoint ep)
        => ep.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : (ep.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None);

    public async Task<(ServerEndpoint imap, ServerEndpoint smtp, string providerHint)> DiscoverAsync(string email, string? providerHint, CancellationToken ct)
    {
        var domain = email.Split('@').LastOrDefault() ?? "";
        if (Presets.TryGetValue(domain, out var preset))
            return (preset.imap, preset.smtp, providerHint ?? domain);

        // эвристика: пробуем типичные хосты, валидируем через TLS IMAP connect
        var candidates = new List<ServerEndpoint>
        {
            new($"imap.{domain}", 993, true, false),
            new($"mail.{domain}", 993, true, false),
            new($"{domain}", 993, true, false),
            new($"imap.{domain}", 143, false, true),
            new($"mail.{domain}", 143, false, true)
        };

        foreach (var imap in candidates)
        {
            if (await CanConnectImapAsync(imap, ct))
            {
                // smtp кандидаты зеркально
                var smtpCandidates = new List<ServerEndpoint>
                {
                    new($"smtp.{domain}", 587, false, true),
                    new($"mail.{domain}", 587, false, true),
                    new($"smtp.{domain}", 465, true, false),
                    new($"mail.{domain}", 465, true, false),
                    new($"{domain}", 587, false, true),
                };

                // не проверяем smtp тут жёстко, вернём первый разумный
                var smtp = smtpCandidates.First();
                return (imap, smtp, providerHint ?? "custom");
            }
        }

        // fallback: пусть пользователь потом руками поправит в будущем ui, сейчас вернём дефолт
        return (
            new ServerEndpoint($"imap.{domain}", 993, true, false),
            new ServerEndpoint($"smtp.{domain}", 587, false, true),
            providerHint ?? "custom"
        );
    }

    public async Task ValidateAsync(string email, string? password, ServerEndpoint imap, ServerEndpoint smtp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("password required");

        try
        {
            using var imapClient = new ImapClient();
            imapClient.Timeout = 8000;
            await imapClient.ConnectAsync(imap.Host, imap.Port, ToSocketOptions(imap), ct);
            await imapClient.AuthenticateAsync(email, password, ct);
            await imapClient.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"IMAP validation failed: {ex.Message}");
        }

        try
        {
            using var smtpClient = new SmtpClient();
            smtpClient.Timeout = 8000;
            await smtpClient.ConnectAsync(smtp.Host, smtp.Port, ToSocketOptions(smtp), ct);
            await smtpClient.AuthenticateAsync(email, password, ct);
            await smtpClient.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"SMTP validation failed: {ex.Message}");
        }
    }

    private static async Task<bool> CanConnectImapAsync(ServerEndpoint ep, CancellationToken ct)
    {
        try
        {
            using var client = new ImapClient();
            client.Timeout = 8000;

            await client.ConnectAsync(ep.Host, ep.Port, ToSocketOptions(ep), ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
