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

    [HttpGet]
    public ActionResult<AccountConfig[]> List()
        => Ok(_store.ListAccounts().ToArray());

    [HttpPost]
    public async Task<ActionResult<CreateAccountResponse>> Create([FromBody] CreateAccountRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("email required");

        var id = Guid.NewGuid().ToString("n");
        var (imap, smtp, hint) = await _discover.DiscoverAsync(req.Email, req.ProviderHint, ct);

        var config = new AccountConfig(
            id,
            req.Email,
            string.IsNullOrWhiteSpace(req.DisplayName) ? req.Email : req.DisplayName,
            hint,
            imap,
            smtp,
            null
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
