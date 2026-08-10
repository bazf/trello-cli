namespace TrelloCli.Credentials;

public sealed class WindowsCredentialStore : ICredentialStore
{
    private readonly IWindowsCredentialManager _credentialManager;

    public WindowsCredentialStore(IWindowsCredentialManager credentialManager)
    {
        _credentialManager = credentialManager;
    }

    public WindowsCredentialStore() : this(new WindowsCredentialManager())
    {
    }

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(_credentialManager.Read("trello-cli", "trello-token"));
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
            _credentialManager.Write("trello-cli", "trello-token", token, localMachinePersistence: true);
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
            _credentialManager.Delete("trello-cli", "trello-token");
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
