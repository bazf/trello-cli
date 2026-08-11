using TrelloCli;
using TrelloCli.Credentials;
using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

try
{
    var application = new CliApplication(
        () => ConfigService.CreateDefaultAsync(warningWriter: Console.Error.WriteLine),
        new ConsoleSecretReader(),
        Console.Out,
        Console.Error,
        new TrelloCliServiceFactory(Console.Out));
    await application.RunAsync(args);
}
catch (CredentialStoreException ex) when (ex.Category == CredentialStoreErrorCategory.StoreUnavailable)
{
    OutputFormatter.Print(ApiResponse<object>.Fail(
        "The operating system credential store is unavailable.",
        "CREDENTIAL_STORE_UNAVAILABLE"));
}
catch (CredentialStoreException)
{
    OutputFormatter.Print(ApiResponse<object>.Fail(
        "The operating system credential store could not complete the operation.",
        "CREDENTIAL_STORE_ERROR"));
}
catch
{
    OutputFormatter.Print(ApiResponse<object>.Fail("The command could not be completed.", "ERROR"));
}
