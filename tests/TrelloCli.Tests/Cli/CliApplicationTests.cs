using TrelloCli;
using TrelloCli.Credentials;
using TrelloCli.Services;
using TrelloCli.Models;
using Xunit;

namespace TrelloCli.Tests.Cli;

public class CliApplicationTests
{
    [Fact]
    public async Task SetAuth_ReadsTheSecretWithoutWritingItToEitherTranscript()
    {
        const string canaryToken = "cli-secret-canary";
        var store = new InMemoryCredentialStore();
        using var configFile = new TemporaryConfigFile();
        var config = new ConfigService(store, _ => null, configFile.Path, _ => { });
        await config.LoadAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success(canaryToken)),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--set-auth", "api-key"]);

        Assert.Equal(canaryToken, store.Token);
        Assert.Contains("Authentication saved", output.ToString());
        Assert.DoesNotContain(canaryToken, output.ToString());
        Assert.DoesNotContain(canaryToken, error.ToString());
    }

    [Fact]
    public async Task SetAuth_RejectsAnExtraTokenArgumentBeforeReadingOrWritingIt()
    {
        const string rejectedCanary = "rejected-token-canary";
        var store = new InMemoryCredentialStore();
        using var configFile = new TemporaryConfigFile();
        var config = new ConfigService(store, _ => null, configFile.Path, _ => { });
        await config.LoadAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var secretReader = new CountingSecretReader(SecretReadResult.Success("prompted-token-canary"));
        var application = new CliApplication(
            config,
            secretReader,
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--set-auth", "api-key", rejectedCanary]);

        Assert.Equal(0, secretReader.ReadCount);
        Assert.Null(store.Token);
        Assert.Contains("TOKEN_ARGUMENT_REJECTED", output.ToString());
        Assert.DoesNotContain(rejectedCanary, output.ToString());
        Assert.DoesNotContain(rejectedCanary, error.ToString());
    }

    [Fact]
    public async Task SetAuth_ReportsNoninteractiveInputWithEnvironmentGuidance()
    {
        var store = new InMemoryCredentialStore();
        using var configFile = new TemporaryConfigFile();
        var config = new ConfigService(store, _ => null, configFile.Path, _ => { });
        await config.LoadAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var secretReader = new CountingSecretReader(SecretReadResult.InteractiveRequired());
        var application = new CliApplication(
            config,
            secretReader,
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--set-auth", "api-key"]);

        Assert.Equal(1, secretReader.ReadCount);
        Assert.Null(store.Token);
        Assert.Contains("INTERACTIVE_REQUIRED", output.ToString());
        Assert.Contains("TRELLO_API_KEY", output.ToString());
        Assert.Contains("TRELLO_TOKEN", output.ToString());
    }

    [Fact]
    public async Task ClearAuth_ReportsPersistedCleanupAndRemainingEnvironmentOverrides()
    {
        var store = new InMemoryCredentialStore("saved-token");
        using var configFile = new TemporaryConfigFile();
        var config = new ConfigService(store, name => name == "TRELLO_API_KEY" ? "environment-key" : null, configFile.Path, _ => { });
        await config.LoadAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--clear-auth"]);

        Assert.Null(store.Token);
        Assert.Contains("Persisted authentication cleared", output.ToString());
        Assert.Contains("\"environmentOverridesRemainActive\":true", output.ToString());
    }

    [Fact]
    public async Task SetAuth_MapsAnUnavailableCredentialStoreWithoutLeakingTokenOrExceptionDetails()
    {
        const string tokenCanary = "store-secret-canary";
        const string exceptionCanary = "credential-store-details-canary";
        var store = new ThrowingCredentialStore(CredentialStoreErrorCategory.StoreUnavailable, exceptionCanary);
        using var configFile = new TemporaryConfigFile();
        var config = new ConfigService(store, _ => null, configFile.Path, _ => { });
        await config.LoadAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success(tokenCanary)),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--set-auth", "api-key"]);

        Assert.Contains("CREDENTIAL_STORE_UNAVAILABLE", output.ToString());
        Assert.DoesNotContain(tokenCanary, output.ToString());
        Assert.DoesNotContain(tokenCanary, error.ToString());
        Assert.DoesNotContain(exceptionCanary, output.ToString());
        Assert.DoesNotContain(exceptionCanary, error.ToString());
    }

    [Fact]
    public async Task SetAuth_MapsCredentialStoreOperationFailuresToTheStableErrorCode()
    {
        var store = new ThrowingCredentialStore(CredentialStoreErrorCategory.OperationFailed, "credential-store-details-canary");
        using var configFile = new TemporaryConfigFile();
        var config = new ConfigService(store, _ => null, configFile.Path, _ => { });
        await config.LoadAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("store-secret-canary")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--set-auth", "api-key"]);

        Assert.Contains("CREDENTIAL_STORE_ERROR", output.ToString());
        Assert.DoesNotContain("credential-store-details-canary", output.ToString());
        Assert.DoesNotContain("store-secret-canary", output.ToString());
    }

    [Fact]
    public async Task ClearAuth_MapsCredentialStoreFailuresWithoutLeakingExceptionDetails()
    {
        const string exceptionCanary = "credential-store-details-canary";
        var store = new ThrowingCredentialStore(CredentialStoreErrorCategory.StoreUnavailable, exceptionCanary);
        using var configFile = new TemporaryConfigFile();
        var config = new ConfigService(store, _ => null, configFile.Path, _ => { });
        await config.LoadAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--clear-auth"]);

        Assert.Contains("CREDENTIAL_STORE_UNAVAILABLE", output.ToString());
        Assert.DoesNotContain(exceptionCanary, output.ToString());
        Assert.DoesNotContain(exceptionCanary, error.ToString());
    }

    [Fact]
    public async Task RunAsync_PreservesRepresentativeNonAuthCommandArgumentsForTheExistingDispatcher()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var dispatcher = new RecordingCommandDispatcher();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory(commandDispatcher: dispatcher));

        await application.RunAsync(["--create-card", "list-id", "Card name", "--desc", "Details"]);

        Assert.Equal(["--create-card", "list-id", "Card name", "--desc", "Details"], Assert.Single(dispatcher.Commands));
    }

    [Fact]
    public async Task CheckAuth_UsesTheResolvedAuthenticationPathInsteadOfTheCommandDispatcher()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var checker = new FixedAuthenticationChecker();
        var dispatcher = new RecordingCommandDispatcher();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory(checker, dispatcher));

        await application.RunAsync(["--check-auth"]);

        Assert.Equal(1, checker.CallCount);
        Assert.Empty(dispatcher.Commands);
        Assert.Contains("member-id", output.ToString());
    }

    [Fact]
    public async Task Help_DescribesInteractiveSetupWithoutPositionalTokenOrConfigJsonClaims()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--help"]);

        Assert.Contains("trello-cli --set-auth <api-key>", output.ToString());
        Assert.DoesNotContain("<api-key> <token>", output.ToString());
        Assert.DoesNotContain("config.json", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{\"ok\":false,\"error\":\"...\",\"code\":\"...\"}", output.ToString());
    }

    [Fact]
    public async Task Help_ExplainsPlatformCredentialStorageAndLinuxPrerequisites()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--help"]);

        Assert.Contains("Windows Credential Manager", output.ToString());
        Assert.Contains("macOS Keychain", output.ToString());
        Assert.Contains("Linux Secret Service", output.ToString());
        Assert.Contains("libsecret-tools", output.ToString());
        Assert.Contains("D-Bus", output.ToString());
    }

    [Fact]
    public async Task Help_ExplainsMigrationCleanupAndHeadlessSemantics()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--help"]);

        Assert.Contains("Legacy plaintext tokens are removed only after secure-store verification", output.ToString());
        Assert.Contains("Environment variables remain active after --clear-auth", output.ToString());
        Assert.Contains("For headless use, set both TRELLO_API_KEY and TRELLO_TOKEN", output.ToString());
    }

    [Fact]
    public async Task RunAsync_ShowsHelpWhenNoCommandIsProvided()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync([]);

        Assert.Contains("USAGE:", output.ToString());
    }

    [Fact]
    public async Task RunAsync_ShowsTheAssemblyPackageVersionForTheVersionFlag()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--version"]);

        Assert.Equal($"trello-cli v2.0.0{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public async Task DefaultServiceFactory_PreservesUnknownCommandOutput()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new TrelloCliServiceFactory(output));

        await application.RunAsync(["--unknown-command"]);

        Assert.Contains("UNKNOWN_COMMAND", output.ToString());
        Assert.Contains("Unknown command: --unknown-command", output.ToString());
    }

    [Fact]
    public async Task RunAsync_DoesNotRecommendTheRemovedPositionalTokenSyntaxWhenAuthenticationIsMissing()
    {
        var config = new ConfigService(
            new InMemoryCredentialStore(),
            _ => null,
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-cli-tests-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--get-boards"]);

        Assert.Contains("AUTH_ERROR", output.ToString());
        Assert.Contains("--set-auth", output.ToString());
        Assert.DoesNotContain("\\u003Capi-key\\u003E \\u003Ctoken\\u003E", output.ToString());
    }

    [Fact]
    public async Task CheckAuth_MapsAStoreFailureEncounteredDuringCredentialResolution()
    {
        const string exceptionCanary = "load-credential-store-details-canary";
        var config = new ConfigService(
            new ThrowingCredentialStore(CredentialStoreErrorCategory.StoreUnavailable, exceptionCanary),
            _ => null,
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-cli-tests-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory());

        await application.RunAsync(["--check-auth"]);

        Assert.Contains("CREDENTIAL_STORE_UNAVAILABLE", output.ToString());
        Assert.DoesNotContain(exceptionCanary, output.ToString());
        Assert.DoesNotContain(exceptionCanary, error.ToString());
    }

    [Fact]
    public async Task RunAsync_SanitizesUnexpectedTopLevelExceptions()
    {
        const string exceptionCanary = "top-level-exception-canary";
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new RecordingServiceFactory(commandDispatcher: new ThrowingCommandDispatcher(exceptionCanary)));

        await application.RunAsync(["--get-boards"]);

        Assert.Contains("\"code\":\"ERROR\"", output.ToString());
        Assert.DoesNotContain(exceptionCanary, output.ToString());
        Assert.DoesNotContain(exceptionCanary, error.ToString());
    }

    [Fact]
    public async Task RunAsync_MapsTopLevelCredentialStoreExceptionsWithoutLeakingDetails()
    {
        const string exceptionCanary = "top-level-credential-store-canary";
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new ThrowingServiceFactory(new CredentialStoreException(CredentialStoreErrorCategory.StoreUnavailable, exceptionCanary)));

        await application.RunAsync(["--get-boards"]);

        Assert.Contains("CREDENTIAL_STORE_UNAVAILABLE", output.ToString());
        Assert.DoesNotContain(exceptionCanary, output.ToString());
        Assert.DoesNotContain(exceptionCanary, error.ToString());
    }

    [Fact]
    public async Task RunAsync_MapsTopLevelCredentialStoreOperationFailuresToTheStableErrorCode()
    {
        var config = await CreateConfiguredServiceAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(SecretReadResult.Success("unused-token")),
            output,
            error,
            new ThrowingServiceFactory(new CredentialStoreException(CredentialStoreErrorCategory.OperationFailed, "credential-store-details-canary")));

        await application.RunAsync(["--get-boards"]);

        Assert.Contains("CREDENTIAL_STORE_ERROR", output.ToString());
        Assert.DoesNotContain("credential-store-details-canary", output.ToString());
    }

    private sealed class FixedSecretReader(SecretReadResult result) : ISecretReader
    {
        public SecretReadResult ReadToken(TextWriter errorWriter) => result;
    }

    private sealed class CountingSecretReader(SecretReadResult result) : ISecretReader
    {
        public int ReadCount { get; private set; }

        public SecretReadResult ReadToken(TextWriter errorWriter)
        {
            ReadCount++;
            return result;
        }
    }

    private static async Task<ConfigService> CreateConfiguredServiceAsync()
    {
        var config = new ConfigService(
            new InMemoryCredentialStore(),
            name => name switch
            {
                "TRELLO_API_KEY" => "environment-api-key",
                "TRELLO_TOKEN" => "environment-token",
                _ => null
            },
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-cli-tests-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        return config;
    }

    private sealed class RecordingServiceFactory(
        IAuthenticationChecker? authenticationChecker = null,
        ICommandDispatcher? commandDispatcher = null) : ICliServiceFactory
    {
        public CliServices Create(ConfigService config) => new(
            authenticationChecker ?? new FixedAuthenticationChecker(),
            commandDispatcher ?? new RecordingCommandDispatcher());
    }

    private sealed class FixedAuthenticationChecker : IAuthenticationChecker
    {
        public int CallCount { get; private set; }

        public Task<ApiResponse<object>> CheckAuthAsync()
        {
            CallCount++;
            return Task.FromResult(ApiResponse<object>.Success(new { id = "member-id" }));
        }
    }

    private sealed class RecordingCommandDispatcher : ICommandDispatcher
    {
        public List<string[]> Commands { get; } = [];

        public Task ExecuteAsync(string[] args)
        {
            Commands.Add(args);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCommandDispatcher(string message) : ICommandDispatcher
    {
        public Task ExecuteAsync(string[] args) => throw new InvalidOperationException(message);
    }

    private sealed class ThrowingServiceFactory(Exception exception) : ICliServiceFactory
    {
        public CliServices Create(ConfigService config) => throw exception;
    }

    private sealed class InMemoryCredentialStore(string? token = null) : ICredentialStore
    {
        public string? Token { get; private set; } = token;

        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(Token);

        public Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            Token = token;
            return Task.CompletedTask;
        }

        public Task DeleteTokenAsync(CancellationToken cancellationToken = default)
        {
            Token = null;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCredentialStore(CredentialStoreErrorCategory category, string message) : ICredentialStore
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<string?>(new CredentialStoreException(category, message));

        public Task SetTokenAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromException(new CredentialStoreException(category, message));

        public Task DeleteTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromException(new CredentialStoreException(category, message));
    }

    private sealed class TemporaryConfigFile : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-cli-tests-{Guid.NewGuid():N}");

        public TemporaryConfigFile()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "config.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }
}
