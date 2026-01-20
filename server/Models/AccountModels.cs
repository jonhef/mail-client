namespace MailClient.Server.Models;

public record ServerEndpoint(string Host, int Port, bool UseSsl, bool UseStartTls);

public record AccountSecrets(
    string? Password,      // для OAuth тут будет access/refresh, пока не реализуем полностью
    string? OAuthAccessToken,
    string? OAuthRefreshToken
);

public record AccountConfig(
    string Id,
    string Email,
    string DisplayName,
    string ProviderHint,     // gmail/outlook/custom etc
    ServerEndpoint Imap,
    ServerEndpoint Smtp,
    ServerEndpoint? Pop3
);

public record StoredAccount(
    AccountConfig Config,
    string EncryptedSecretsBase64
);
