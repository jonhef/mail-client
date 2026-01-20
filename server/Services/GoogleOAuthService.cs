using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using MailClient.Server.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MailClient.Server.Services;

public sealed class GoogleOAuthService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly AccountStore _accounts;
    private readonly ILogger<GoogleOAuthService> _logger;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _redirectUri;

    private sealed record GoogleAuthSession(string Email, string? DisplayName, string CodeVerifier, string ProviderHint);
    private sealed record TokenResponse(string access_token, int expires_in, string token_type, string? refresh_token);
    private sealed record UserInfoResponse(string email, bool email_verified, string? name);

    public GoogleOAuthService(IHttpClientFactory httpClientFactory, IMemoryCache cache, IConfiguration config, AccountStore accounts, ILogger<GoogleOAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _accounts = accounts;
        _logger = logger;

        _clientId = config["GoogleOAuth:ClientId"];
        _clientSecret = config["GoogleOAuth:ClientSecret"];
        _redirectUri = config["GoogleOAuth:RedirectUri"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_redirectUri);

    public Task<GoogleOAuthStartResponse> StartAsync(GoogleOAuthStartRequest req, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google OAuth is not configured. Set GoogleOAuth:ClientId and GoogleOAuth:RedirectUri.");

        var clientId = _clientId!;
        var redirectUri = _redirectUri!;

        var state = Guid.NewGuid().ToString("N");
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = ToCodeChallenge(codeVerifier);

        var uri = new UriBuilder("https://accounts.google.com/o/oauth2/v2/auth");
        var scope = Uri.EscapeDataString("https://mail.google.com/ https://www.googleapis.com/auth/userinfo.email");
        uri.Query =
            $"client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&access_type=offline" +
            $"&include_granted_scopes=true" +
            $"&prompt=consent" +
            $"&scope={scope}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256";

        _cache.Set(state, new GoogleAuthSession(req.Email, req.DisplayName, codeVerifier, "gmail-oauth"), TimeSpan.FromMinutes(10));

        return Task.FromResult(new GoogleOAuthStartResponse(uri.ToString()));
    }

    public async Task<(AccountConfig account, AccountSecrets secrets)> CompleteAsync(string state, string code, CancellationToken ct)
    {
        if (!_cache.TryGetValue<GoogleAuthSession>(state, out var session) || session is null)
            throw new InvalidOperationException("OAuth state not found or expired.");

        _cache.Remove(state);

        var token = await ExchangeCodeAsync(code, session.CodeVerifier, ct);
        var user = await FetchUserAsync(token.access_token, ct);

        var email = string.IsNullOrWhiteSpace(session.Email) ? user.email : session.Email;
        var displayName = !string.IsNullOrWhiteSpace(session.DisplayName) ? session.DisplayName : (user.name ?? email);

        var config = new AccountConfig(
            Guid.NewGuid().ToString("n"),
            email,
            displayName,
            session.ProviderHint,
            new ServerEndpoint("imap.gmail.com", 993, true, false),
            new ServerEndpoint("smtp.gmail.com", 587, false, true),
            null
        );

        var secrets = new AccountSecrets(
            null,
            token.access_token,
            token.refresh_token,
            DateTimeOffset.UtcNow.AddSeconds(token.expires_in)
        );

        _accounts.Upsert(config, secrets);

        return (config, secrets);
    }

    public async Task<AccountSecrets> RefreshAsync(AccountConfig config, AccountSecrets secrets, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secrets.OAuthRefreshToken))
            throw new InvalidOperationException("Refresh token missing.");

        try
        {
            var refreshed = await RefreshTokenAsync(secrets.OAuthRefreshToken, ct);
            var updated = secrets with
            {
                OAuthAccessToken = refreshed.access_token,
                OAuthRefreshToken = refreshed.refresh_token ?? secrets.OAuthRefreshToken,
                OAuthAccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshed.expires_in)
            };
            _accounts.Upsert(config, updated);
            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh token for {Email}", config.Email);
            throw;
        }
    }

    private async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google OAuth is not configured.");

        var clientId = _clientId!;
        var redirectUri = _redirectUri!;
        var clientSecret = _clientSecret ?? string.Empty;

        var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("code_verifier", codeVerifier),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("redirect_uri", redirectUri)
        });

        var resp = await client.PostAsync(TokenEndpoint, content, ct);
        resp.EnsureSuccessStatusCode();
        var parsed = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        if (parsed is null || string.IsNullOrWhiteSpace(parsed.access_token))
            throw new InvalidOperationException("Failed to exchange code for token.");
        return parsed;
    }

    private async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google OAuth is not configured.");

        var clientId = _clientId!;
        var clientSecret = _clientSecret ?? string.Empty;

        var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("grant_type", "refresh_token")
        });

        var resp = await client.PostAsync(TokenEndpoint, content, ct);
        resp.EnsureSuccessStatusCode();
        var parsed = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        if (parsed is null || string.IsNullOrWhiteSpace(parsed.access_token))
            throw new InvalidOperationException("Failed to refresh token.");
        return parsed;
    }

    private async Task<UserInfoResponse> FetchUserAsync(string accessToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var parsed = await resp.Content.ReadFromJsonAsync<UserInfoResponse>(cancellationToken: ct);
        if (parsed is null || string.IsNullOrWhiteSpace(parsed.email))
            throw new InvalidOperationException("Failed to fetch user info.");
        return parsed;
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    private static string ToCodeChallenge(string codeVerifier)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64Url(hash);
    }

    private static string Base64Url(ReadOnlySpan<byte> data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
