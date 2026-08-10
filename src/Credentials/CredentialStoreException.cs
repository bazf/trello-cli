namespace TrelloCli.Credentials;

public enum CredentialStoreErrorCategory
{
    StoreUnavailable,
    OperationFailed
}

public sealed class CredentialStoreException : Exception
{
    public CredentialStoreException(CredentialStoreErrorCategory category, string message)
        : base(message)
    {
        Category = category;
    }

    public CredentialStoreErrorCategory Category { get; }
}
