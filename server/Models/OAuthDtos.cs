namespace MailClient.Server.Models;

public record GoogleOAuthStartRequest(string Email, string? DisplayName);
public record GoogleOAuthStartResponse(string AuthUrl);
