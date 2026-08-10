namespace TrelloCli.Credentials;

public sealed class LinuxCredentialStore : ICredentialStore
{
    private const string SecretToolPath = "/usr/bin/secret-tool";
    private readonly IProcessRunner _processRunner;
    private readonly string _service;
    private readonly string _account;

    public LinuxCredentialStore(IProcessRunner processRunner)
        : this(processRunner, "trello-cli", "trello-token")
    {
    }

    internal LinuxCredentialStore(IProcessRunner processRunner, string service, string account)
    {
        _processRunner = processRunner;
        _service = service;
        _account = account;
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new ProcessRunRequest(
                SecretToolPath,
                ["lookup", "service", _service, "account", _account],
                null),
            cancellationToken);

        if (IsMissingResult(result) || CredentialStoreProcessHelpers.IsNotFound(result)) return null;
        CredentialStoreProcessHelpers.EnsureSucceeded(result);
        return CredentialStoreProcessHelpers.NormalizeToolNewline(result.StandardOutput);
    }

    public async Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new ProcessRunRequest(
                SecretToolPath,
                ["store", "--label=Trello CLI token", "service", _service, "account", _account],
                token),
            cancellationToken);

        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }

    public async Task DeleteTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new ProcessRunRequest(
                SecretToolPath,
                ["clear", "service", _service, "account", _account],
                null),
            cancellationToken);

        if (IsMissingResult(result) || CredentialStoreProcessHelpers.IsNotFound(result)) return;
        CredentialStoreProcessHelpers.EnsureSucceeded(result);
    }

    private static bool IsMissingResult(ProcessRunResult result) =>
        result.IsAvailable && !result.TimedOut && result.ExitCode == 1 &&
        string.IsNullOrEmpty(result.StandardOutput) &&
        string.IsNullOrEmpty(result.StandardError);
}
