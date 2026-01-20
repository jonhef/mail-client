using MailClient.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace MailClient.Server.Tests;

public class CryptoServiceTests
{
    [Fact]
    public void EncryptAndDecrypt_RoundTrips()
    {
        var provider = DataProtectionProvider.Create("MailClientTests");
        var service = new CryptoService(provider);

        const string secret = "super-secret-value";

        var encrypted = service.EncryptToBase64(secret);
        var decrypted = service.DecryptFromBase64(encrypted);

        Assert.Equal(secret, decrypted);
    }
}
