using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli;

public sealed class CliApplication
{
    private static string Version => typeof(CliApplication).Assembly.GetName().Version!.ToString(3);

    private readonly ConfigService _config;
    private readonly ISecretReader _secretReader;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly ICliServiceFactory _serviceFactory;

    public CliApplication(
        ConfigService config,
        ISecretReader secretReader,
        TextWriter output,
        TextWriter error,
        ICliServiceFactory serviceFactory)
    {
        _config = config;
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
            ShowHelp();
            return;
        }

        if (args[0] == "--version" || args[0] == "-v")
        {
            _output.WriteLine($"trello-cli v{Version}");
            return;
        }

        if (args[0] == "--clear-auth")
        {
            var (success, clearError, environmentOverridesRemainActive) = await _config.ClearAuthAsync();
            if (success)
                Write(ApiResponse<object>.Success(
                    new ClearAuthSuccessData("Persisted authentication cleared", environmentOverridesRemainActive)));
            else
                WriteCredentialStoreFailure(clearError!, "CLEAR_ERROR");
            return;
        }

        if (args[0] != "--set-auth")
        {
            var (valid, validationError) = _config.Validate();
            if (!valid)
            {
                WriteCredentialStoreFailure(validationError!, "AUTH_ERROR");
                return;
            }

            var services = _serviceFactory.Create(_config);
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

        var (saveSuccess, authError) = await _config.SaveAuthAsync(args[1], secretResult.Token!);
        if (saveSuccess)
            Write(ApiResponse<object>.Success(new { message = "Authentication saved in the operating system credential store." }));
        else
            WriteCredentialStoreFailure(authError!, "SAVE_ERROR");
    }

    private void WriteCredentialStoreFailure(string error, string fallbackCode)
    {
        if (_config.LastCredentialStoreError == Credentials.CredentialStoreErrorCategory.StoreUnavailable)
        {
            Write(ApiResponse<object>.Fail("The operating system credential store is unavailable.", "CREDENTIAL_STORE_UNAVAILABLE"));
            return;
        }

        if (_config.LastCredentialStoreError == Credentials.CredentialStoreErrorCategory.OperationFailed)
        {
            Write(ApiResponse<object>.Fail("The operating system credential store could not complete the operation.", "CREDENTIAL_STORE_ERROR"));
            return;
        }

        Write(ApiResponse<object>.Fail(error, fallbackCode));
    }

    private void ShowHelp()
    {
        _output.WriteLine($@"trello-cli v{Version}
CLI tool for Trello with AI-friendly JSON output

USAGE:
  trello-cli <command> [arguments] [options]

AUTHENTICATION:
  Option 1 - CLI (recommended):
    trello-cli --set-auth <api-key>
    You will be securely prompted for your Trello token.

  Option 2 - Environment variables:
    TRELLO_API_KEY  - Your Trello API key
    TRELLO_TOKEN    - Your Trello token

  Storage:
    Windows Credential Manager; macOS Keychain; Linux Secret Service.
    Linux requires secret-tool (libsecret-tools) and a running D-Bus Secret Service.

  Semantics:
    Environment credentials override persisted credentials.
    Legacy plaintext tokens are removed only after secure-store verification.
    Migration failure preserves the legacy file and reports a safe warning.
    Environment variables remain active after --clear-auth; it does not revoke tokens.
    For headless use, set both TRELLO_API_KEY and TRELLO_TOKEN.

  Get credentials: https://trello.com/app-key

COMMANDS:
  --help, -h                          Show this help
  --version, -v                       Show version
  --set-auth <api-key>                Save authentication using the operating system credential store
  --clear-auth                        Remove persisted authentication
  --check-auth                        Verify API credentials

  Board:
    --get-boards            List all boards
    --get-board <id>        Get specific board

  List:
    --get-lists <board-id>              Get lists in a board
    --create-list <board-id> <name>     Create new list
    --move-list <list-id> <pos>         Move list to position (top, bottom, or a number)
    --bulk-move-lists <id:pos>...       Move multiple lists, e.g. id1:top id2:bottom

  Card:
    --get-cards <list-id>               Get cards in a list
    --get-all-cards <board-id>          Get all cards in a board
    --get-card <card-id>                Get specific card
    --create-card <list-id> <name>      Create card
      [--desc <description>]
      [--due <date>]
      [--labels <ids>]                  Comma-separated label IDs
      [--members <ids>]                 Comma-separated member IDs
    --update-card <card-id>             Update card
      [--name <name>]
      [--desc <description>]
      [--due <date>]
      [--labels <ids>]
      [--members <ids>]
      [--closed <true|false>]           Archive/unarchive card
    --archive-card <card-id>            Archive card (shortcut for --update-card --closed true)
    --unarchive-card <card-id>          Unarchive card (shortcut for --update-card --closed false)
    --move-card <card-id> <list-id>     Move card to list
    --delete-card <card-id>             Delete card
    --get-comments <card-id>            Get comments on a card
    --add-comment <card-id> <text>      Add comment to a card

  Attachment:
    --list-attachments <card-id>                 List attachments on a card
    --upload-attachment <card-id> <file-path>    Upload file as attachment
      [--name <name>]                            Custom attachment name
    --attach-url <card-id> <url>                 Attach URL to card
      [--name <name>]                            Custom attachment name
    --delete-attachment <card-id> <attach-id>    Delete attachment

  Note: Downloading attachments is not supported. Trello's download API
  requires browser authentication. Use --attach-url to link attachments.

  Label:
    --get-labels <board-id>                          List labels on a board
    --create-label <board-id> <name>                 Create label on a board
      [--color <color>]                              Trello color name (e.g. green, red, blue, orange...)
    --update-label <label-id>                        Update label
      [--name <name>]
      [--color <color>]
    --delete-label <label-id>                        Delete label

  Checklist:
    --get-checklists <card-id>                          Get checklists on a card
    --create-checklist <card-id> <name>                 Create checklist on a card
    --delete-checklist <checklist-id>                   Delete a checklist
    --add-checklist-item <checklist-id> <name>          Add item to checklist
    --update-checklist-item <card-id> <item-id> <state> Mark item complete/incomplete
    --delete-checklist-item <checklist-id> <item-id>    Delete item from checklist

OUTPUT:
  All responses are JSON: {{""ok"":true,""data"":...}} or {{""ok"":false,""error"":""..."",""code"":""...""}}

EXAMPLES:
  trello-cli --get-boards
  trello-cli --get-board abc123
  trello-cli --create-card xyz789 ""My Task"" --desc ""Details""
  trello-cli --move-card card123 list456
");
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
