namespace TrelloCli.Credentials;

public sealed class WindowsCredentialStore : ICredentialStore
{
    private readonly IWindowsCredentialManager _credentialManager;
    private readonly string _target;
    private readonly string _username;

    public WindowsCredentialStore(IWindowsCredentialManager credentialManager)
        : this(credentialManager, "trello-cli", "trello-token")
    {
    }

    internal WindowsCredentialStore(IWindowsCredentialManager credentialManager, string target, string username)
    {
        _credentialManager = credentialManager;
        _target = target;
        _username = username;
    }

    public WindowsCredentialStore() : this(new WindowsCredentialManager())
    {
    }

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(_credentialManager.Read(_target, _username));
        }
        catch (PlatformNotSupportedException)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.StoreUnavailable,
                "The credential store is unavailable.");
        }
        catch (Exception)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.OperationFailed,
                "The credential-store operation failed.");
        }
    }

    public Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _credentialManager.Write(_target, _username, token, localMachinePersistence: true);
            return Task.CompletedTask;
        }
        catch (PlatformNotSupportedException)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.StoreUnavailable,
                "The credential store is unavailable.");
        }
        catch (Exception)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.OperationFailed,
                "The credential-store operation failed.");
        }
    }

    public Task DeleteTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _credentialManager.Delete(_target, _username);
            return Task.CompletedTask;
        }
        catch (PlatformNotSupportedException)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.StoreUnavailable,
                "The credential store is unavailable.");
        }
        catch (Exception)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCategory.OperationFailed,
                "The credential-store operation failed.");
        }
    }
}
