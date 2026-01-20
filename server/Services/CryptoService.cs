using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace MailClient.Server.Services;

public sealed class CryptoService
{
    private readonly IDataProtector _protector;

    public CryptoService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("mailclient.secrets.v1");
    }

    public string EncryptToBase64(string plaintext)
    {
        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(protectedBytes);
    }

    public string DecryptFromBase64(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var unprotected = _protector.Unprotect(bytes);
        return Encoding.UTF8.GetString(unprotected);
    }
}
