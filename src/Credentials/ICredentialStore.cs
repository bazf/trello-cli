namespace TrelloCli.Credentials;

public interface ICredentialStore
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);

    Task SetTokenAsync(string token, CancellationToken cancellationToken = default);

    Task DeleteTokenAsync(CancellationToken cancellationToken = default);
}
