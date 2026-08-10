namespace TrelloCli.Credentials;

public sealed class LinuxCredentialStore(IProcessRunner processRunner) : ICredentialStore
{
    private const string SecretToolPath = "/usr/bin/secret-tool";

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            new ProcessRunRequest(
                SecretToolPath,
                ["lookup", "service", "trello-cli", "account", "trello-token"],
                null),
            cancellationToken);

        if (CredentialStoreProcessHelpers.IsNotFound(result)) return null;
        CredentialStoreProcessHelpers.EnsureSucceeded(result);
        return CredentialStoreProcessHelpers.NormalizeToolNewline(result.StandardOutput);
    }

    public async Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            new ProcessRunRequest(
                SecretToolPath,
                ["store", "--label=Trello CLI token", "service", "trello-cli", "account", "trello-token"],
                token),
            cancellationToken);

        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }

    public async Task DeleteTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            new ProcessRunRequest(
                SecretToolPath,
                ["clear", "service", "trello-cli", "account", "trello-token"],
                null),
            cancellationToken);

        if (CredentialStoreProcessHelpers.IsNotFound(result)) return;
        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }
}
