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

    [Theory]
    [InlineData("user@icloud.com", "imap.mail.me.com", "smtp.mail.me.com")]
    [InlineData("user@me.com", "imap.mail.me.com", "smtp.mail.me.com")]
    [InlineData("user@mac.com", "imap.mail.me.com", "smtp.mail.me.com")]
    public async Task Discover_ReturnsIcloudPresets(string email, string imapHost, string smtpHost)
    {
        var svc = new AutodiscoverService();
        var (imap, smtp, hint) = await svc.DiscoverAsync(email, null, CancellationToken.None);

        Assert.Equal(imapHost, imap.Host);
        Assert.Equal(smtpHost, smtp.Host);
        Assert.Equal("icloud", hint);
    }
}
