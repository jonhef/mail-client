using MailClient.Server.Models;
using MailKit.Net.Imap;
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

    private static async Task<bool> CanConnectImapAsync(ServerEndpoint ep, CancellationToken ct)
    {
        try
        {
            using var client = new ImapClient();
            client.Timeout = 8000;

            var opts = ep.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : (ep.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None);

            await client.ConnectAsync(ep.Host, ep.Port, opts, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
