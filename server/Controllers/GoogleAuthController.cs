using MailClient.Server.Models;
using MailClient.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MailClient.Server.Controllers;

[ApiController]
[Route("api/oauth/google")]
public sealed class GoogleAuthController : ControllerBase
{
    private readonly GoogleOAuthService _googleOAuth;

    public GoogleAuthController(GoogleOAuthService googleOAuth)
    {
        _googleOAuth = googleOAuth;
    }

    [HttpPost("start")]
    public async Task<ActionResult<GoogleOAuthStartResponse>> Start([FromBody] GoogleOAuthStartRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("email required");

        if (!_googleOAuth.IsConfigured)
            return StatusCode(503, "Google OAuth not configured");

        var res = await _googleOAuth.StartAsync(req, ct);
        return Ok(res);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? state, [FromQuery] string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
            return BadRequest("missing state or code");

        try
        {
            var (config, _) = await _googleOAuth.CompleteAsync(state, code, ct);
            return Content(BuildCloseHtml(true, $"Linked {config.Email}"), "text/html");
        }
        catch (Exception ex)
        {
            return Content(BuildCloseHtml(false, ex.Message), "text/html");
        }
    }

    private static string BuildCloseHtml(bool success, string message)
    {
        var safeMsg = System.Net.WebUtility.HtmlEncode(message);
        var payload = $"{{\"type\":\"OAUTH_DONE\",\"success\":{success.ToString().ToLowerInvariant()},\"message\":\"{safeMsg}\"}}";
        return $$"""
<!doctype html>
<html>
<body>
<script>
  (function() {
    const payload = {{payload}};
    if (window.opener) {
      window.opener.postMessage(payload, "*");
    }
    document.write(payload.message || "done");
    setTimeout(() => window.close(), 500);
  })();
</script>
</body>
</html>
""";
    }
}
