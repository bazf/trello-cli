using TrelloCli.Credentials;

namespace TrelloCli.Tests.Credentials;

internal sealed class RecordingWindowsCredentialManager : IWindowsCredentialManager
{
    public string? TokenToRead { get; set; }
    public Exception? ExceptionToThrow { get; set; }
    public bool DeleteResult { get; set; } = true;
    public List<(string Target, string Username, string Token, bool LocalMachine)> Writes { get; } = [];
    public List<(string Target, string Username)> Deletes { get; } = [];

    public string? Read(string target, string username)
    {
        ThrowIfNeeded();
        return TokenToRead;
    }

    public void Write(string target, string username, string token, bool localMachinePersistence)
    {
        ThrowIfNeeded();
        Writes.Add((target, username, token, localMachinePersistence));
    }

    public bool Delete(string target, string username)
    {
        ThrowIfNeeded();
        Deletes.Add((target, username));
        return DeleteResult;
    }

    private void ThrowIfNeeded()
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
    }
}
