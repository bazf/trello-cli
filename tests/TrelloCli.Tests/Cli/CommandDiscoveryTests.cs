using System.Text.Json;
using TrelloCli;
using TrelloCli.Credentials;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Cli;

/// <summary>
/// Guards the discovery surface: --help, --help &lt;command&gt; and --commands must describe
/// every command the CLI actually accepts, together with its limits.
/// </summary>
public class CommandDiscoveryTests
{
    [Fact]
    public void Catalog_CoversExactlyTheCommandsTheDispatcherAccepts()
    {
        var dispatcher = CreateDispatcher();

        var dispatched = CommandCatalog.DispatchedCommands.Select(command => command.Name).ToHashSet(StringComparer.Ordinal);
        var accepted = dispatcher.KnownCommands.ToHashSet(StringComparer.Ordinal);

        Assert.Empty(accepted.Except(dispatched)); // accepted but undocumented
        Assert.Empty(dispatched.Except(accepted)); // documented but not accepted
    }

    [Fact]
    public void Catalog_DescribesEveryCommandWithAUsageLineAndAnExample()
    {
        Assert.Equal(
            CommandCatalog.Commands.Select(command => command.Name).Distinct(StringComparer.Ordinal).Count(),
            CommandCatalog.Commands.Count);

        foreach (var command in CommandCatalog.Commands)
        {
            Assert.StartsWith("--", command.Name);
            Assert.False(string.IsNullOrWhiteSpace(command.Summary), $"{command.Name} has no summary");
            Assert.NotEmpty(command.Examples);
            Assert.Contains(command.Group, CommandCatalog.GroupOrder);
            Assert.StartsWith($"trello-cli {command.Name}", command.Usage);
        }
    }

    [Fact]
    public async Task Help_ListsEveryCommandAndTheGlobalRestrictions()
    {
        var help = await RunAsync(["--help"]);

        foreach (var command in CommandCatalog.Commands)
            Assert.Contains(command.Name, help);

        Assert.Contains("LIMITS AND RESTRICTIONS:", help);
        Assert.Contains("Not supported (no command exists):", help);
        Assert.Contains("Irreversible operations:", help);
        Assert.Contains("ERROR CODES:", help);
        Assert.Contains("UNKNOWN_COMMAND", help);
    }

    [Fact]
    public async Task Help_PointsAtPerCommandHelpAndTheJsonCatalog()
    {
        var help = await RunAsync(["--help"]);

        Assert.Contains("DISCOVERY:", help);
        Assert.Contains("trello-cli --help <command>", help);
        Assert.Contains("trello-cli --commands", help);
    }

    [Fact]
    public async Task Help_ForASingleCommand_ShowsUsageOptionsAndItsOwnLimits()
    {
        var help = await RunAsync(["--help", "--update-card"]);

        Assert.Contains("trello-cli --update-card <card-id> [--name <text>]", help);
        Assert.Contains("OPTIONS:", help);
        Assert.Contains("--closed <true|false>", help);
        Assert.Contains("LIMITS AND BEHAVIOR:", help);
        Assert.Contains("NO_PARAMS", help);
        Assert.Contains("EXAMPLES:", help);
    }

    [Fact]
    public async Task Help_ForASingleCommand_AcceptsTheNameWithoutLeadingDashes()
    {
        Assert.Equal(
            await RunAsync(["--help", "--get-boards"]),
            await RunAsync(["--help", "get-boards"]));
    }

    [Fact]
    public async Task Help_ForAnUnknownCommand_ReturnsAJsonErrorThatSuggestsTheClosestMatch()
    {
        var output = await RunAsync(["--help", "--create-crd"]);

        Assert.Contains("\"code\":\"UNKNOWN_COMMAND\"", output);
        Assert.Contains("--create-card", output);
        Assert.Contains("--commands", output);
    }

    [Fact]
    public async Task Commands_ReturnsAMachineReadableCatalogOfEveryCommand()
    {
        using var document = JsonDocument.Parse(await RunAsync(["--commands"]));
        var root = document.RootElement;
        var data = root.GetProperty("data");

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("trello-cli", data.GetProperty("tool").GetString());

        var described = data.GetProperty("commands").EnumerateArray()
            .Select(command => command.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            CommandCatalog.Commands.Select(command => command.Name).ToHashSet(StringComparer.Ordinal),
            described);

        foreach (var command in data.GetProperty("commands").EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(command.GetProperty("usage").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(command.GetProperty("summary").GetString()));
        }

        Assert.NotEmpty(data.GetProperty("restrictions").EnumerateArray());
        Assert.NotEmpty(data.GetProperty("discovery").EnumerateArray());
        Assert.Contains(
            data.GetProperty("errorCodes").EnumerateArray().Select(code => code.GetProperty("code").GetString()),
            code => code == "UNKNOWN_COMMAND");
        Assert.Contains("exits with code 0", data.GetProperty("output").GetProperty("exitCode").GetString());
    }

    [Fact]
    public async Task Commands_ForASingleCommand_ReturnsThatCommandOnly()
    {
        using var document = JsonDocument.Parse(await RunAsync(["--commands", "--upload-attachment"]));
        var data = document.RootElement.GetProperty("data");

        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("--upload-attachment", data.GetProperty("name").GetString());
        Assert.Contains(
            data.GetProperty("notes").EnumerateArray().Select(note => note.GetString()),
            note => note!.Contains("FILE_NOT_FOUND", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Commands_ForAnUnknownCommand_ReportsUnknownCommand()
    {
        var output = await RunAsync(["--commands", "--not-a-command"]);

        Assert.Contains("\"code\":\"UNKNOWN_COMMAND\"", output);
        Assert.Contains("--help", output);
    }

    [Fact]
    public async Task Dispatcher_ForAnUnknownCommand_PointsAtTheDiscoveryCommands()
    {
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher(output);

        await dispatcher.ExecuteAsync(["--get-crds"]);

        var text = output.ToString();
        Assert.Contains("\"code\":\"UNKNOWN_COMMAND\"", text);
        Assert.Contains("--get-cards", text);
        Assert.Contains("trello-cli --help", text);
        Assert.Contains("trello-cli --commands", text);
    }

    [Fact]
    public async Task UnknownCommand_IsReportedBeforeCredentialsAreEvenLoaded()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            () => throw new InvalidOperationException("credential-loading-canary"),
            new UnusedSecretReader(),
            output,
            error,
            new UnusedServiceFactory());

        await application.RunAsync(["--get-crds", "list-id"]);

        var text = output.ToString();
        Assert.Contains("\"code\":\"UNKNOWN_COMMAND\"", text);
        Assert.Contains("--get-cards", text);
        Assert.DoesNotContain("credential-loading-canary", text);
        Assert.DoesNotContain("AUTH_ERROR", text);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--commands")]
    public async Task DiscoveryCommands_WorkWithoutCredentials(string command)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            () => throw new InvalidOperationException("credential-loading-canary"),
            new UnusedSecretReader(),
            output,
            error,
            new UnusedServiceFactory());

        await application.RunAsync([command]);

        Assert.DoesNotContain("credential-loading-canary", output.ToString());
        Assert.Contains("--get-boards", output.ToString());
    }

    private static async Task<string> RunAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            await CreateConfigAsync(),
            new UnusedSecretReader(),
            output,
            error,
            new UnusedServiceFactory());

        await application.RunAsync(args);
        return output.ToString();
    }

    private static CommandDispatcher CreateDispatcher(TextWriter? output = null) =>
        new(new TrelloApiService(CreateConfigAsync().GetAwaiter().GetResult(), new HttpClient(new UnusedHandler())),
            output ?? TextWriter.Null);

    private static async Task<ConfigService> CreateConfigAsync()
    {
        var config = new ConfigService(
            new UnusedCredentialStore(),
            name => name switch
            {
                "TRELLO_API_KEY" => "environment-api-key",
                "TRELLO_TOKEN" => "environment-token",
                _ => null
            },
            Path.Combine(Path.GetTempPath(), $"trello-cli-discovery-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        return config;
    }

    private sealed class UnusedCredentialStore : ICredentialStore
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SetTokenAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class UnusedSecretReader : ISecretReader
    {
        public SecretReadResult ReadToken(TextWriter errorWriter) =>
            throw new InvalidOperationException("Discovery commands must not read secrets.");
    }

    private sealed class UnusedServiceFactory : ICliServiceFactory
    {
        public CliServices Create(ConfigService config) =>
            throw new InvalidOperationException("Discovery commands must not reach Trello.");
    }

    private sealed class UnusedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Discovery tests must not send requests.");
    }
}
