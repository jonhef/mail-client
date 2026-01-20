using System.Collections.Generic;
using MailClient.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MailClient.Server.Tests;

public class CryptoServiceTests
{
    [Fact]
    public void EncryptAndDecrypt_RoundTrips()
    {
        var provider = DataProtectionProvider.Create("MailClientTests");
        var config = new ConfigurationBuilder().Build();
        var service = new CryptoService(provider, config);

        const string secret = "super-secret-value";

        var encrypted = service.EncryptToBase64(secret);
        var decrypted = service.DecryptFromBase64(encrypted);

        Assert.Equal(secret, decrypted);
    }

    [Fact]
    public void MasterPassword_RoundTrips()
    {
        var provider = DataProtectionProvider.Create("MailClientTests");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:MasterPassword"] = "horse-staple"
            })
            .Build();
        var service = new CryptoService(provider, config);

        const string secret = "super-secret-value";

        var encrypted = service.EncryptToBase64(secret);
        Assert.StartsWith("mp1:", encrypted);

        var decrypted = service.DecryptFromBase64(encrypted);
        Assert.Equal(secret, decrypted);
    }
}
