using MailClient.Server.Models;
using MailClient.Server.Services;
using Xunit;

namespace MailClient.Server.Tests;

public class AutodiscoverServiceTests
{
    [Fact]
    public async Task ValidateAsync_ThrowsWithoutPassword()
    {
        var svc = new AutodiscoverService();
        var imap = new ServerEndpoint("imap.example.com", 993, true, false);
        var smtp = new ServerEndpoint("smtp.example.com", 587, false, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ValidateAsync("user@example.com", null, imap, smtp, CancellationToken.None));
    }
}
