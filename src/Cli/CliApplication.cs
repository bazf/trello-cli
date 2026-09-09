using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli;

public sealed class CliApplication
{
    private static string Version => typeof(CliApplication).Assembly.GetName().Version!.ToString(3);

    private readonly Func<Task<ConfigService>> _configFactory;
    private readonly ISecretReader _secretReader;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly ICliServiceFactory _serviceFactory;

    public CliApplication(
        ConfigService config,
        ISecretReader secretReader,
        TextWriter output,
        TextWriter error,
        ICliServiceFactory serviceFactory) : this(
            () => Task.FromResult(config),
            secretReader,
            output,
            error,
            serviceFactory)
    {
    }

    internal CliApplication(
        Func<Task<ConfigService>> configFactory,
        ISecretReader secretReader,
        TextWriter output,
        TextWriter error,
        ICliServiceFactory serviceFactory)
    {
        _configFactory = configFactory;
        _secretReader = secretReader;
        _output = output;
        _error = error;
        _serviceFactory = serviceFactory;
    }

    public async Task RunAsync(string[] args)
    {
        try
        {
            await RunCoreAsync(args);
        }
        catch (Credentials.CredentialStoreException ex)
            when (ex.Category == Credentials.CredentialStoreErrorCategory.StoreUnavailable)
        {
            Write(ApiResponse<object>.Fail("The operating system credential store is unavailable.", "CREDENTIAL_STORE_UNAVAILABLE"));
        }
        catch (Credentials.CredentialStoreException)
        {
            Write(ApiResponse<object>.Fail("The operating system credential store could not complete the operation.", "CREDENTIAL_STORE_ERROR"));
        }
        catch
        {
            Write(ApiResponse<object>.Fail("The command could not be completed.", "ERROR"));
        }
    }

    private async Task RunCoreAsync(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            ShowHelp(args.Length > 1 ? args[1] : null);
            return;
        }

        if (args[0] == "--commands")
        {
            ShowCommandCatalog(args.Length > 1 ? args[1] : null);
            return;
        }

        if (args[0] == "--version" || args[0] == "-v")
        {
            _output.WriteLine($"trello-cli v{Version}");
            return;
        }

        // Rejected before any credential work so a typo reports the typo, not an auth error.
        if (!CommandCatalog.Contains(args[0]))
        {
            Write(ApiResponse<object>.Fail(CommandCatalog.DescribeUnknownCommand(args[0]), "UNKNOWN_COMMAND"));
            return;
        }

        var config = await _configFactory();

        if (args[0] == "--clear-auth")
        {
            var (success, clearError, environmentOverridesRemainActive) = await config.ClearAuthAsync();
            if (success)
                Write(ApiResponse<object>.Success(
                    new ClearAuthSuccessData("Persisted authentication cleared", environmentOverridesRemainActive)));
            else
                WriteCredentialStoreFailure(config, clearError!, "CLEAR_ERROR");
            return;
        }

        if (args[0] != "--set-auth")
        {
            var (valid, validationError) = config.Validate();
            if (!valid)
            {
                WriteCredentialStoreFailure(config, validationError!, "AUTH_ERROR");
                return;
            }

            var services = _serviceFactory.Create(config);
            if (args[0] == "--check-auth")
            {
                Write(await services.AuthenticationChecker.CheckAuthAsync());
                return;
            }

            await services.CommandDispatcher.ExecuteAsync(args);
            return;
        }

        if (args.Length < 2)
        {
            Write(ApiResponse<object>.Fail("Usage: trello-cli --set-auth <api-key>", "MISSING_PARAM"));
            return;
        }

        if (args.Length > 2)
        {
            Write(ApiResponse<object>.Fail("Enter the Trello token interactively instead of passing it as an argument.", "TOKEN_ARGUMENT_REJECTED"));
            return;
        }

        var secretResult = _secretReader.ReadToken(_error);
        if (!secretResult.IsSuccess)
        {
            Write(ApiResponse<object>.Fail(secretResult.Error!, secretResult.ErrorCode!));
            return;
        }

        var (saveSuccess, authError) = await config.SaveAuthAsync(args[1], secretResult.Token!);
        if (saveSuccess)
            Write(ApiResponse<object>.Success(new { message = "Authentication saved in the operating system credential store." }));
        else
            WriteCredentialStoreFailure(config, authError!, "SAVE_ERROR");
    }

    private void WriteCredentialStoreFailure(ConfigService config, string error, string fallbackCode)
    {
        if (config.LastCredentialStoreError == Credentials.CredentialStoreErrorCategory.StoreUnavailable)
        {
            Write(ApiResponse<object>.Fail("The operating system credential store is unavailable.", "CREDENTIAL_STORE_UNAVAILABLE"));
            return;
        }

        if (config.LastCredentialStoreError == Credentials.CredentialStoreErrorCategory.OperationFailed)
        {
            Write(ApiResponse<object>.Fail("The operating system credential store could not complete the operation.", "CREDENTIAL_STORE_ERROR"));
            return;
        }

        Write(ApiResponse<object>.Fail(error, fallbackCode));
    }

    private void ShowHelp(string? commandName)
    {
        if (commandName is null)
        {
            _output.Write(HelpRenderer.RenderOverview(Version));
            return;
        }

        var command = CommandCatalog.Find(commandName);
        if (command is null)
        {
            Write(ApiResponse<object>.Fail(CommandCatalog.DescribeUnknownCommand(commandName), "UNKNOWN_COMMAND"));
            return;
        }

        _output.Write(HelpRenderer.RenderCommand(Version, command));
    }

    private void ShowCommandCatalog(string? commandName)
    {
        if (commandName is null)
        {
            Write(ApiResponse<CommandManifest>.Success(CommandCatalog.BuildManifest(Version)));
            return;
        }

        var command = CommandCatalog.Find(commandName);
        if (command is null)
        {
            Write(ApiResponse<object>.Fail(CommandCatalog.DescribeUnknownCommand(commandName), "UNKNOWN_COMMAND"));
            return;
        }

        Write(ApiResponse<CommandDefinition>.Success(command));
    }

    private void Write<T>(T response) => _output.WriteLine(OutputFormatter.ToJson(response));
}

public interface ISecretReader
{
    SecretReadResult ReadToken(TextWriter errorWriter);
}

public sealed record SecretReadResult(string? Token, string? Error = null, string? ErrorCode = null)
{
    public bool IsSuccess => ErrorCode is null;

    public static SecretReadResult Success(string token) => new(token);

    public static SecretReadResult InteractiveRequired() => new(
        null,
        "Interactive token entry requires a terminal. Set TRELLO_API_KEY and TRELLO_TOKEN for noninteractive use.",
        "INTERACTIVE_REQUIRED");

    public static SecretReadResult Empty() => new(null, "Trello token cannot be empty.", "TOKEN_REQUIRED");

    public static SecretReadResult Cancelled() => new(null, "Trello token entry was cancelled.", "TOKEN_INPUT_CANCELLED");
}

public interface IAuthenticationChecker
{
    Task<ApiResponse<object>> CheckAuthAsync();
}

public interface ICommandDispatcher
{
    Task ExecuteAsync(string[] args);
}

public sealed record CliServices(IAuthenticationChecker AuthenticationChecker, ICommandDispatcher CommandDispatcher);

public interface ICliServiceFactory
{
    CliServices Create(ConfigService config);
}
