using MailClient.Server.Models;
using MailClient.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MailClient.Server.Controllers;

[ApiController]
[Route("api/mail")]
public sealed class MailController : ControllerBase
{
    private readonly MailService _mail;

    public MailController(MailService mail)
    {
        _mail = mail;
    }

    [HttpGet("{accountId}/folders")]
    public async Task<ActionResult<FolderDto[]>> Folders(string accountId, CancellationToken ct)
        => Ok(await _mail.ListFoldersAsync(accountId, ct));

    [HttpGet("{accountId}/messages")]
    public async Task<ActionResult<ListMessagesResponse>> Messages(
        string accountId,
        [FromQuery] string folderId,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 10, 200);
        return Ok(await _mail.ListMessagesAsync(accountId, folderId, cursor, pageSize, ct));
    }

    [HttpGet("{accountId}/message")]
    public async Task<ActionResult<MessageDto>> Message(
        string accountId,
        [FromQuery] string messageId,
        CancellationToken ct)
    {
        return Ok(await _mail.GetMessageAsync(accountId, messageId, ct));
    }

    [HttpPatch("{accountId}/message")]
    public async Task<ActionResult> Update(
        string accountId,
        [FromQuery] string messageId,
        [FromBody] UpdateMessageRequest req,
        CancellationToken ct)
    {
        await _mail.UpdateMessageAsync(accountId, messageId, req, ct);
        return NoContent();
    }

    [HttpPost("{accountId}/send")]
    public async Task<ActionResult> Send(
        string accountId,
        [FromBody] SendEmailRequest req,
        CancellationToken ct)
    {
        await _mail.SendEmailAsync(accountId, req, ct);
        return Accepted();
    }
}
