namespace TrelloCli.Credentials;

public sealed class MacOsCredentialStore(IProcessRunner processRunner) : ICredentialStore
{
    private const string SecurityPath = "/usr/bin/security";

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            new ProcessRunRequest(
                SecurityPath,
                ["find-generic-password", "-a", "trello-token", "-s", "trello-cli", "-w"],
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
                SecurityPath,
                ["add-generic-password", "-U", "-a", "trello-token", "-s", "trello-cli", "-w"],
                token),
            cancellationToken);

        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }

    public async Task DeleteTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            new ProcessRunRequest(
                SecurityPath,
                ["delete-generic-password", "-a", "trello-token", "-s", "trello-cli"],
                null),
            cancellationToken);

        if (CredentialStoreProcessHelpers.IsNotFound(result)) return;
        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }
}
