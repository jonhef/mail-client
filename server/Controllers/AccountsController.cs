using MailClient.Server.Models;
using MailClient.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MailClient.Server.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly AccountStore _store;
    private readonly AutodiscoverService _discover;

    public AccountsController(AccountStore store, AutodiscoverService discover)
    {
        _store = store;
        _discover = discover;
    }

    [HttpPost("discover")]
    public async Task<ActionResult<DiscoverResponse>> Discover([FromBody] DiscoverRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("email required");

        var (imap, smtp, hint) = await _discover.DiscoverAsync(req.Email, req.ProviderHint, ct);
        return Ok(new DiscoverResponse(imap, smtp, hint));
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ValidateSettingsResponse>> Validate([FromBody] ValidateSettingsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("email required");

        try
        {
            await _discover.ValidateAsync(req.Email, req.Password, req.Imap, req.Smtp, ct);
            return Ok(new ValidateSettingsResponse(true, "connection ok"));
        }
        catch (Exception ex)
        {
            return BadRequest(new ValidateSettingsResponse(false, ex.Message));
        }
    }

    [HttpGet]
    public ActionResult<AccountConfig[]> List()
        => Ok(_store.ListAccounts().ToArray());

    [HttpPost]
    public async Task<ActionResult<CreateAccountResponse>> Create([FromBody] CreateAccountRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("email required");

        var id = Guid.NewGuid().ToString("n");
        ServerEndpoint imap, smtp;
        string hint = req.ProviderHint ?? "custom";

        if (req.Imap is null || req.Smtp is null)
        {
            (imap, smtp, hint) = await _discover.DiscoverAsync(req.Email, req.ProviderHint, ct);
        }
        else
        {
            imap = req.Imap;
            smtp = req.Smtp;
        }

        var config = new AccountConfig(
            id,
            req.Email,
            string.IsNullOrWhiteSpace(req.DisplayName) ? req.Email : req.DisplayName,
            hint,
            imap,
            smtp,
            req.Pop3
        );

        var secrets = new AccountSecrets(req.Password, null, null);
        _store.Upsert(config, secrets);

        return Ok(new CreateAccountResponse(config));
    }

    [HttpDelete("{accountId}")]
    public ActionResult Delete(string accountId)
    {
        _store.Delete(accountId);
        return NoContent();
    }
}
