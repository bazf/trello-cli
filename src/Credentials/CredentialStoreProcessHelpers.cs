namespace TrelloCli.Credentials;

internal static class CredentialStoreProcessHelpers
{
    public static bool IsNotFound(ProcessRunResult result) =>
        result.IsAvailable && !result.TimedOut && result.ExitCode != 0 &&
        string.IsNullOrEmpty(result.StandardOutput) &&
        (result.StandardError.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
         result.StandardError.Contains("could not be found", StringComparison.OrdinalIgnoreCase));

    public static void EnsureSucceeded(ProcessRunResult result)
    {
        if (!result.IsAvailable)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.StoreUnavailable,
                "The credential store is unavailable.");
        }

        if (result.TimedOut)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.OperationFailed,
                "The credential-store operation timed out.");
        }

        if (result.ExitCode != 0)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.OperationFailed,
                "The credential-store operation failed.");
        }
    }

    public static string NormalizeToolNewline(string value) =>
        value.EndsWith('\n') ? value[..^1] : value;
}
