using TrelloCli.Credentials;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class WindowsCredentialStoreTests
{
    [Fact]
    public async Task SetTokenAsync_UsesInjectedIdentifiersForAnIsolatedCredential()
    {
        var manager = new RecordingWindowsCredentialManager();
        var store = new WindowsCredentialStore(manager, "trello-cli-smoke-123", "trello-token-smoke-123");

        await store.SetTokenAsync("windows-token-canary");

        Assert.Equal(
            ("trello-cli-smoke-123", "trello-token-smoke-123", "windows-token-canary", true),
            Assert.Single(manager.Writes));
    }

    [Fact]
    public async Task SetTokenAsync_UsesTheExactTargetUsernameAndLocalMachinePersistence()
    {
        const string token = "windows-token-canary";
        var manager = new RecordingWindowsCredentialManager();
        var store = new WindowsCredentialStore(manager);

        await store.SetTokenAsync(token);

        var write = Assert.Single(manager.Writes);
        Assert.Equal(("trello-cli", "trello-token", token, true), write);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsTheStoredTokenWithoutAlteration()
    {
        var manager = new RecordingWindowsCredentialManager { TokenToRead = "token\n" };
        var store = new WindowsCredentialStore(manager);

        var token = await store.GetTokenAsync();

        Assert.Equal("token\n", token);
    }

    [Fact]
    public async Task DeleteTokenAsync_IsIdempotentWhenTheCredentialDoesNotExist()
    {
        var manager = new RecordingWindowsCredentialManager { DeleteResult = false };
        var store = new WindowsCredentialStore(manager);

        await store.DeleteTokenAsync();

        Assert.Equal(("trello-cli", "trello-token"), Assert.Single(manager.Deletes));
    }

    [Fact]
    public async Task GetTokenAsync_ConvertsManagerErrorsToSanitizedOperationalFailures()
    {
        const string secret = "windows-token-canary";
        var manager = new RecordingWindowsCredentialManager { ExceptionToThrow = new InvalidOperationException(secret) };
        var store = new WindowsCredentialStore(manager);

        var exception = await Assert.ThrowsAsync<CredentialStoreException>(() => store.GetTokenAsync());

        Assert.Equal(CredentialStoreErrorCategory.OperationFailed, exception.Category);
        Assert.DoesNotContain(secret, exception.Message);
    }
}
