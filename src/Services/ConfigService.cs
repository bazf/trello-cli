using System.Text.Json;
using TrelloCli.Credentials;

namespace TrelloCli.Services;

public class ConfigService
{
    private static readonly string DefaultConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".trello-cli"
    );
    private static readonly string DefaultConfigFile = Path.Combine(DefaultConfigDir, "config.json");

    public string? ApiKey { get; private set; }
    public string? Token { get; private set; }
    public bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Token);

    private readonly ICredentialStore? _credentialStore;
    private readonly Func<string, string?>? _environmentReader;
    private readonly string? _configPath;
    private readonly Action<string>? _warningWriter;

    public ConfigService(
        ICredentialStore credentialStore,
        Func<string, string?> environmentReader,
        string configPath,
        Action<string> warningWriter)
    {
        _credentialStore = credentialStore;
        _environmentReader = environmentReader;
        _configPath = configPath;
        _warningWriter = warningWriter;
    }

    public static async Task<ConfigService> CreateDefaultAsync()
    {
        var service = new ConfigService(
            new CredentialStoreFactory().Create(),
            Environment.GetEnvironmentVariable,
            DefaultConfigFile,
            Console.Error.WriteLine);
        await service.LoadAsync();
        return service;
    }

    public async Task LoadAsync()
    {
        ApiKey = Nonblank(_environmentReader!("TRELLO_API_KEY"));
        Token = Nonblank(_environmentReader("TRELLO_TOKEN"));

        var config = await ReadConfigAsync(_configPath!);
        ApiKey ??= config?.ApiKey;

        if (Token is not null) return;

        var legacyToken = Nonblank(config?.Token);
        if (legacyToken is not null)
        {
            Token = legacyToken;
            try
            {
                await _credentialStore!.SetTokenAsync(legacyToken);
                var verifiedToken = await _credentialStore.GetTokenAsync();
                if (!string.Equals(legacyToken, verifiedToken, StringComparison.Ordinal))
                    throw new InvalidOperationException();

                await WriteConfigAtomicallyAsync(_configPath!, new ConfigData { ApiKey = config!.ApiKey });
            }
            catch
            {
                Warn("Unable to migrate the saved Trello token.");
            }

            return;
        }

        try
        {
            Token = await _credentialStore!.GetTokenAsync();
        }
        catch
        {
            Warn("Unable to access the saved Trello token.");
        }
    }

    public async Task<(bool success, string? error)> SaveAuthAsync(string apiKey, string token)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return (false, "API Key cannot be empty");
        if (string.IsNullOrWhiteSpace(token)) return (false, "Token cannot be empty");

        try
        {
            _ = await SnapshotConfigAsync(_configPath!);
        }
        catch
        {
            return (false, "Unable to save authentication.");
        }

        string? previousToken;
        try
        {
            previousToken = await _credentialStore!.GetTokenAsync();
        }
        catch
        {
            return (false, "Unable to save authentication.");
        }

        try
        {
            await _credentialStore.SetTokenAsync(token);
            var verifiedToken = await _credentialStore.GetTokenAsync();
            if (!string.Equals(token, verifiedToken, StringComparison.Ordinal))
                throw new InvalidOperationException();

            await WriteConfigAtomicallyAsync(_configPath!, new ConfigData { ApiKey = apiKey });
            ApiKey = apiKey;
            Token = token;
            return (true, null);
        }
        catch
        {
            try
            {
                if (previousToken is null)
                    await _credentialStore.DeleteTokenAsync();
                else
                    await _credentialStore.SetTokenAsync(previousToken);
            }
            catch
            {
                Warn("Unable to restore the saved Trello token.");
            }

            return (false, "Unable to save authentication.");
        }
    }

    public async Task<(bool success, string? error, bool environmentOverridesRemainActive)> ClearAuthAsync()
    {
        var storeDeleted = true;
        var configDeleted = true;

        try
        {
            await _credentialStore!.DeleteTokenAsync();
        }
        catch
        {
            storeDeleted = false;
            Warn("Unable to remove the saved Trello token.");
        }

        try
        {
            if (File.Exists(_configPath)) File.Delete(_configPath);
        }
        catch
        {
            configDeleted = false;
            Warn("Unable to remove saved Trello configuration.");
        }

        var success = storeDeleted && configDeleted;
        return (
            success,
            success ? null : "Authentication was only partially cleared.",
            Nonblank(_environmentReader!("TRELLO_API_KEY")) is not null ||
            Nonblank(_environmentReader("TRELLO_TOKEN")) is not null);
    }

    public string GetAuthQuery()
    {
        return $"key={ApiKey}&token={Token}";
    }

    public (bool valid, string? error) Validate()
    {
        if (string.IsNullOrEmpty(ApiKey))
            return (false, "API Key not set. Use: trello-cli --set-auth <api-key> <token>");

        if (string.IsNullOrEmpty(Token))
            return (false, "Token not set. Use: trello-cli --set-auth <api-key> <token>");

        return (true, null);
    }

    private class ConfigData
    {
        public string? ApiKey { get; set; }
        public string? Token { get; set; }
    }

    private async Task<ConfigData?> ReadConfigAsync(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ConfigData>(await File.ReadAllTextAsync(path));
        }
        catch
        {
            Warn("Unable to read saved Trello configuration.");
            return null;
        }
    }

    private static async Task<byte[]?> SnapshotConfigAsync(string path) =>
        File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;

    private static async Task WriteConfigAtomicallyAsync(string configPath, ConfigData config)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (string.IsNullOrEmpty(directory)) throw new IOException();

        Directory.CreateDirectory(directory);
        EnsureSecureDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(configPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(config));
            EnsureSecureFile(temporaryPath);
            File.Move(temporaryPath, configPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    private static void EnsureSecureDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void EnsureSecureFile(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private void Warn(string message) => _warningWriter?.Invoke(message);

    private static string? Nonblank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
