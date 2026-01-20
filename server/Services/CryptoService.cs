using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace MailClient.Server.Services;

public sealed class CryptoService
{
    private const string MasterPrefix = "mp1:";
    private readonly IDataProtector _protector;
    private readonly MasterPasswordProtector? _masterProtector;

    public CryptoService(IDataProtectionProvider provider, IConfiguration configuration)
    {
        _protector = provider.CreateProtector("mailclient.secrets.v1");

        var masterPassword = configuration.GetValue<string>("Secrets:MasterPassword");
        if (!string.IsNullOrWhiteSpace(masterPassword))
        {
            _masterProtector = new MasterPasswordProtector(masterPassword);
        }
    }

    public string EncryptToBase64(string plaintext)
    {
        if (_masterProtector is not null)
        {
            return $"{MasterPrefix}{_masterProtector.Encrypt(plaintext)}";
        }

        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(protectedBytes);
    }

    public string DecryptFromBase64(string base64)
    {
        if (base64.StartsWith(MasterPrefix, StringComparison.Ordinal))
        {
            if (_masterProtector is null)
                throw new InvalidOperationException("Master password is required to decrypt secrets.");

            return _masterProtector.Decrypt(base64.Substring(MasterPrefix.Length));
        }

        var bytes = Convert.FromBase64String(base64);
        var unprotected = _protector.Unprotect(bytes);
        return Encoding.UTF8.GetString(unprotected);
    }

    private sealed class MasterPasswordProtector
    {
        private readonly byte[] _passwordBytes;

        public MasterPasswordProtector(string password)
        {
            _passwordBytes = Encoding.UTF8.GetBytes(password);
        }

        public string Encrypt(string plaintext)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var key = DeriveKey(salt);

            using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);

            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipher = new byte[plainBytes.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            aes.Encrypt(nonce, plainBytes, cipher, tag);

            var payload = new byte[salt.Length + nonce.Length + tag.Length + cipher.Length];
            Buffer.BlockCopy(salt, 0, payload, 0, salt.Length);
            Buffer.BlockCopy(nonce, 0, payload, salt.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, payload, salt.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(cipher, 0, payload, salt.Length + nonce.Length + tag.Length, cipher.Length);

            CryptographicOperations.ZeroMemory(key);
            return Convert.ToBase64String(payload);
        }

        public string Decrypt(string base64)
        {
            var payload = Convert.FromBase64String(base64);

            var salt = payload.AsSpan(0, 16).ToArray();
            var nonce = payload.AsSpan(16, AesGcm.NonceByteSizes.MaxSize).ToArray();
            var tag = payload.AsSpan(16 + AesGcm.NonceByteSizes.MaxSize, AesGcm.TagByteSizes.MaxSize).ToArray();
            var cipher = payload.AsSpan(16 + AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize).ToArray();

            var key = DeriveKey(salt);
            using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            var plaintext = new byte[cipher.Length];

            aes.Decrypt(nonce, cipher, tag, plaintext);

            CryptographicOperations.ZeroMemory(key);
            return Encoding.UTF8.GetString(plaintext);
        }

        private byte[] DeriveKey(byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(_passwordBytes, salt, 600_000, HashAlgorithmName.SHA256);
            return kdf.GetBytes(32);
        }
    }
}
