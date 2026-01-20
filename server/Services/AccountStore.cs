using System.Text.Json;
using MailClient.Server.Models;

namespace MailClient.Server.Services;

public sealed class AccountStore
{
    private readonly string _path;
    private readonly CryptoService _crypto;
    private readonly object _lock = new();

    public AccountStore(IConfiguration cfg, IWebHostEnvironment env, CryptoService crypto)
    {
        _crypto = crypto;
        var rel = cfg.GetValue<string>("AccountStorePath") ?? "Data/account-store.json";
        _path = Path.Combine(env.ContentRootPath, rel);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        if (!File.Exists(_path))
            File.WriteAllText(_path, "[]");
    }

    public IReadOnlyList<AccountConfig> ListAccounts()
    {
        lock (_lock)
        {
            var stored = LoadInternal();
            return stored.Select(s => s.Config).ToList();
        }
    }

    public StoredAccount GetStored(string id)
    {
        lock (_lock)
        {
            var stored = LoadInternal().FirstOrDefault(a => a.Config.Id == id);
            if (stored is null) throw new KeyNotFoundException("account not found");
            return stored;
        }
    }

    public AccountSecrets GetSecrets(string id)
    {
        var stored = GetStored(id);
        var json = _crypto.DecryptFromBase64(stored.EncryptedSecretsBase64);
        return JsonSerializer.Deserialize<AccountSecrets>(json) ?? new AccountSecrets(null, null, null);
    }

    public void Upsert(AccountConfig config, AccountSecrets secrets)
    {
        lock (_lock)
        {
            var list = LoadInternal();
            var json = JsonSerializer.Serialize(secrets);
            var enc = _crypto.EncryptToBase64(json);
            var stored = new StoredAccount(config, enc);

            var idx = list.FindIndex(x => x.Config.Id == config.Id);
            if (idx >= 0) list[idx] = stored;
            else list.Add(stored);

            SaveInternal(list);
        }
    }

    public void Delete(string id)
    {
        lock (_lock)
        {
            var list = LoadInternal();
            list.RemoveAll(x => x.Config.Id == id);
            SaveInternal(list);
        }
    }

    private List<StoredAccount> LoadInternal()
    {
        var text = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<StoredAccount>>(text) ?? new List<StoredAccount>();
    }

    private void SaveInternal(List<StoredAccount> list)
    {
        var text = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, text);
    }
}
