using TrelloCli.Credentials;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Services;

public class ConfigServiceTests
{
    [Fact]
    public async Task LoadAsync_UsesANonblankEnvironmentTokenWithoutCallingTheCredentialStore()
    {
        using var directory = new TemporaryDirectory();
        var store = new RecordingCredentialStore { Token = "stored-token" };
        var service = new ConfigService(
            store,
            name => name == "TRELLO_TOKEN" ? "environment-token" : null,
            Path.Combine(directory.Path, "config.json"),
            _ => { });

        await service.LoadAsync();

        Assert.Equal("environment-token", service.Token);
        Assert.Equal(0, store.GetCalls);
        Assert.Equal(0, store.SetCalls);
    }

    [Fact]
    public async Task LoadAsync_ResolvesTheApiKeyFromConfigurationAndTokenFromTheCredentialStore()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"ApiKey\":\"file-key\"}");
        var store = new RecordingCredentialStore { Token = "stored-token" };
        var service = new ConfigService(store, _ => null, configPath, _ => { });

        await service.LoadAsync();

        Assert.Equal("file-key", service.ApiKey);
        Assert.Equal("stored-token", service.Token);
        Assert.Equal(1, store.GetCalls);
    }

    [Fact]
    public async Task LoadAsync_MigratesALegacyTokenToTheCredentialStoreAndRewritesTheConfigWithoutIt()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"ApiKey\":\"file-key\",\"Token\":\"legacy-token\"}");
        var store = new RecordingCredentialStore();
        var service = new ConfigService(store, _ => null, configPath, _ => { });

        await service.LoadAsync();

        Assert.Equal("legacy-token", service.Token);
        Assert.Equal("legacy-token", store.Token);
        Assert.Equal(1, store.SetCalls);
        Assert.Equal(1, store.GetCalls);
        Assert.DoesNotContain("legacy-token", await File.ReadAllTextAsync(configPath));
        Assert.Contains("file-key", await File.ReadAllTextAsync(configPath));
    }

    [Fact]
    public async Task SaveAuthAsync_WritesAndVerifiesTheTokenAndPersistsOnlyTheApiKey()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        var store = new RecordingCredentialStore();
        var service = new ConfigService(store, _ => null, configPath, _ => { });

        var result = await service.SaveAuthAsync("new-key", "new-token");

        Assert.True(result.success);
        Assert.Equal("new-token", store.Token);
        Assert.Equal(1, store.SetCalls);
        Assert.Equal(2, store.GetCalls);
        var persisted = await File.ReadAllTextAsync(configPath);
        Assert.Contains("new-key", persisted);
        Assert.DoesNotContain("new-token", persisted);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(configPath));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory.Path));
        }
    }

    [Fact]
    public async Task ClearAuthAsync_RemovesSavedCredentialsAndReportsRemainingEnvironmentOverrides()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"ApiKey\":\"file-key\",\"Token\":\"legacy-token\"}");
        var store = new RecordingCredentialStore { Token = "stored-token" };
        var service = new ConfigService(
            store,
            name => name == "TRELLO_TOKEN" ? "environment-token" : null,
            configPath,
            _ => { });

        var result = await service.ClearAuthAsync();

        Assert.True(result.success);
        Assert.True(result.environmentOverridesRemainActive);
        Assert.Null(store.Token);
        Assert.False(File.Exists(configPath));
    }

    [Fact]
    public async Task LoadAsync_WhenMigrationVerificationMismatches_PreservesLegacyBytesAndWarnsWithoutSecrets()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        var original = "{\n  \"ApiKey\": \"file-key\",\n  \"Token\": \"legacy-token\"\n}";
        await File.WriteAllTextAsync(configPath, original);
        var warnings = new List<string>();
        var store = new RecordingCredentialStore { ReadOverride = "different-token" };
        var service = new ConfigService(store, _ => null, configPath, warnings.Add);

        await service.LoadAsync();

        Assert.Equal("legacy-token", service.Token);
        Assert.Equal(original, await File.ReadAllTextAsync(configPath));
        Assert.Single(warnings);
        Assert.DoesNotContain("legacy-token", warnings[0]);
        Assert.DoesNotContain("different-token", warnings[0]);
    }

    [Fact]
    public async Task LoadAsync_WhenMigrationStoreWriteFails_PreservesLegacyBytesAndUsesTheLegacyToken()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        var original = "{\"ApiKey\":\"file-key\",\"Token\":\"legacy-token\"}";
        await File.WriteAllTextAsync(configPath, original);
        var warnings = new List<string>();
        var store = new RecordingCredentialStore { SetException = new InvalidOperationException("store-token-details") };
        var service = new ConfigService(store, _ => null, configPath, warnings.Add);

        await service.LoadAsync();

        Assert.Equal("legacy-token", service.Token);
        Assert.Equal(original, await File.ReadAllTextAsync(configPath));
        Assert.Single(warnings);
        Assert.DoesNotContain("store-token-details", warnings[0]);
    }

    [Fact]
    public async Task LoadAsync_ResolvesTheApiKeyFromEnvironmentIndependentlyOfTheStoredToken()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"ApiKey\":\"file-key\"}");
        var store = new RecordingCredentialStore { Token = "stored-token" };
        var service = new ConfigService(
            store,
            name => name == "TRELLO_API_KEY" ? "environment-key" : null,
            configPath,
            _ => { });

        await service.LoadAsync();

        Assert.Equal("environment-key", service.ApiKey);
        Assert.Equal("stored-token", service.Token);
    }

    [Fact]
    public async Task LoadAsync_WhenTheStoreIsUnavailable_LeavesAuthenticationAsAValidationError()
    {
        using var directory = new TemporaryDirectory();
        var store = new RecordingCredentialStore { GetException = new InvalidOperationException("store-token-details") };
        var warnings = new List<string>();
        var service = new ConfigService(
            store,
            name => name == "TRELLO_API_KEY" ? "environment-key" : null,
            Path.Combine(directory.Path, "config.json"),
            warnings.Add);

        await service.LoadAsync();

        var validation = service.Validate();
        Assert.False(validation.valid);
        Assert.Equal("Token not set. Use: trello-cli --set-auth <api-key> <token>", validation.error);
        Assert.Single(warnings);
        Assert.DoesNotContain("store-token-details", warnings[0]);
    }

    [Fact]
    public async Task LoadAsync_RetriesMigrationAfterAVerificationFailure()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"ApiKey\":\"file-key\",\"Token\":\"legacy-token\"}");
        var store = new RecordingCredentialStore { ReadOverride = "different-token" };
        var firstAttempt = new ConfigService(store, _ => null, configPath, _ => { });

        await firstAttempt.LoadAsync();
        store.ReadOverride = null;
        var retry = new ConfigService(store, _ => null, configPath, _ => { });
        await retry.LoadAsync();

        Assert.Equal("legacy-token", retry.Token);
        Assert.DoesNotContain("legacy-token", await File.ReadAllTextAsync(configPath));
        Assert.Equal(2, store.SetCalls);
    }

    [Fact]
    public async Task LoadAsync_WhenMigrationConfigPersistenceFails_UsesTheLegacyTokenAndCleansTemporaryFiles()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"ApiKey\":\"file-key\",\"Token\":\"legacy-token\"}");
        var warnings = new List<string>();
        var store = new RecordingCredentialStore
        {
            OnGet = () =>
            {
                File.Delete(configPath);
                Directory.CreateDirectory(configPath);
            }
        };
        var service = new ConfigService(store, _ => null, configPath, warnings.Add);

        await service.LoadAsync();

        Assert.Equal("legacy-token", service.Token);
        Assert.Single(warnings);
        Assert.Empty(Directory.GetFiles(directory.Path, ".*.tmp"));
    }

    [Fact]
    public async Task SaveAuthAsync_WhenConfigPersistenceFails_DeletesANewTokenAsRollback()
    {
        using var directory = new TemporaryDirectory();
        var blocker = Path.Combine(directory.Path, "not-a-directory");
        await File.WriteAllTextAsync(blocker, "blocker");
        var store = new RecordingCredentialStore();
        var service = new ConfigService(store, _ => null, Path.Combine(blocker, "config.json"), _ => { });

        var result = await service.SaveAuthAsync("new-key", "new-token");

        Assert.False(result.success);
        Assert.Equal("Unable to save authentication.", result.error);
        Assert.Null(store.Token);
        Assert.Equal(1, store.DeleteCalls);
    }

    [Fact]
    public async Task SaveAuthAsync_WhenConfigPersistenceFails_RestoresThePreviousToken()
    {
        using var directory = new TemporaryDirectory();
        var blocker = Path.Combine(directory.Path, "not-a-directory");
        await File.WriteAllTextAsync(blocker, "blocker");
        var store = new RecordingCredentialStore { Token = "previous-token" };
        var service = new ConfigService(store, _ => null, Path.Combine(blocker, "config.json"), _ => { });

        var result = await service.SaveAuthAsync("new-key", "new-token");

        Assert.False(result.success);
        Assert.Equal("previous-token", store.Token);
        Assert.Equal(2, store.SetCalls);
        Assert.Equal(0, store.DeleteCalls);
    }

    [Fact]
    public async Task SaveAuthAsync_RejectsBlankInputWithoutChangingExistingCredentials()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        const string original = "{\"ApiKey\":\"old-key\"}";
        await File.WriteAllTextAsync(configPath, original);
        var store = new RecordingCredentialStore { Token = "old-token" };
        var service = new ConfigService(store, _ => null, configPath, _ => { });

        var result = await service.SaveAuthAsync(" ", "new-token");

        Assert.False(result.success);
        Assert.Equal("API Key cannot be empty", result.error);
        Assert.Equal("old-token", store.Token);
        Assert.Equal(0, store.GetCalls);
        Assert.Equal(original, await File.ReadAllTextAsync(configPath));
    }

    [Fact]
    public async Task ClearAuthAsync_ReportsPartialFailureWhileStillDeletingTheConfig()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"ApiKey\":\"file-key\",\"Token\":\"legacy-token\"}");
        var store = new RecordingCredentialStore { DeleteException = new InvalidOperationException("delete-token-details") };
        var warnings = new List<string>();
        var service = new ConfigService(store, _ => null, configPath, warnings.Add);

        var result = await service.ClearAuthAsync();

        Assert.False(result.success);
        Assert.Equal("Authentication was only partially cleared.", result.error);
        Assert.False(File.Exists(configPath));
        Assert.Single(warnings);
        Assert.DoesNotContain("delete-token-details", warnings[0]);
    }

    [Fact]
    public async Task ClearAuthAsync_TreatsMissingCredentialsAsSuccessfullyCleared()
    {
        using var directory = new TemporaryDirectory();
        var store = new RecordingCredentialStore();
        var service = new ConfigService(store, _ => null, Path.Combine(directory.Path, "config.json"), _ => { });

        var result = await service.ClearAuthAsync();

        Assert.True(result.success);
        Assert.Null(result.error);
        Assert.False(result.environmentOverridesRemainActive);
        Assert.Equal(1, store.DeleteCalls);
    }

    private sealed class RecordingCredentialStore : ICredentialStore
    {
        public string? Token { get; set; }
        public string? ReadOverride { get; set; }
        public Exception? GetException { get; set; }
        public Exception? SetException { get; set; }
        public Exception? DeleteException { get; set; }
        public Action? OnGet { get; set; }
        public int GetCalls { get; private set; }
        public int SetCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            GetCalls++;
            if (GetException is not null) return Task.FromException<string?>(GetException);
            OnGet?.Invoke();
            return Task.FromResult(ReadOverride ?? Token);
        }

        public Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            SetCalls++;
            if (SetException is not null) return Task.FromException(SetException);
            Token = token;
            return Task.CompletedTask;
        }

        public Task DeleteTokenAsync(CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            if (DeleteException is not null) return Task.FromException(DeleteException);
            Token = null;
            return Task.CompletedTask;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-cli-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
