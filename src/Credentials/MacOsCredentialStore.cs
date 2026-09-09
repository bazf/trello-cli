namespace TrelloCli.Credentials;

public sealed class MacOsCredentialStore : ICredentialStore
{
    private const string SecurityPath = "/usr/bin/security";
    private readonly IProcessRunner _processRunner;
    private readonly string _service;
    private readonly string _account;

    public MacOsCredentialStore(IProcessRunner processRunner)
        : this(processRunner, "trello-cli", "trello-token")
    {
    }

    internal MacOsCredentialStore(IProcessRunner processRunner, string service, string account)
    {
        _processRunner = processRunner;
        _service = service;
        _account = account;
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new ProcessRunRequest(
                SecurityPath,
                ["find-generic-password", "-a", _account, "-s", _service, "-w"],
                null),
            cancellationToken);

        if (CredentialStoreProcessHelpers.IsNotFound(result)) return null;
        CredentialStoreProcessHelpers.EnsureSucceeded(result);
        return CredentialStoreProcessHelpers.NormalizeToolNewline(result.StandardOutput);
    }

    public async Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new ProcessRunRequest(
                SecurityPath,
                ["add-generic-password", "-U", "-a", _account, "-s", _service, "-w"],
                $"{token}\n{token}\n"),
            cancellationToken);

        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }

    public async Task DeleteTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new ProcessRunRequest(
                SecurityPath,
                ["delete-generic-password", "-a", _account, "-s", _service],
                null),
            cancellationToken);

        if (CredentialStoreProcessHelpers.IsNotFound(result)) return;
        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }
}
