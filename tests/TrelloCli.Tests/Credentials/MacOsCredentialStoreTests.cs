using TrelloCli.Credentials;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class MacOsCredentialStoreTests
{
    [Fact]
    public async Task SetTokenAsync_UsesInjectedIdentifiersForAnIsolatedCredential()
    {
        var runner = new RecordingProcessRunner(_ => Success());
        var store = new MacOsCredentialStore(runner, "trello-cli-smoke-123", "trello-token-smoke-123");

        await store.SetTokenAsync("mac-token-canary");

        Assert.Equal(
            ["add-generic-password", "-U", "-a", "trello-token-smoke-123", "-s", "trello-cli-smoke-123", "-w"],
            Assert.Single(runner.Requests).Arguments);
    }

    [Fact]
    public async Task SetTokenAsync_LineTerminatesThePromptInputWithoutPuttingTheSecretInArguments()
    {
        const string token = "mac-token-canary";
        var runner = new RecordingProcessRunner(_ => Success());
        var store = new MacOsCredentialStore(runner);

        await store.SetTokenAsync(token);

        var request = Assert.Single(runner.Requests);
        Assert.Equal("/usr/bin/security", request.FileName);
        Assert.Equal(["add-generic-password", "-U", "-a", "trello-token", "-s", "trello-cli", "-w"], request.Arguments);
        Assert.Equal($"{token}\n{token}\n", request.StandardInput);
        Assert.DoesNotContain(token, request.Arguments);
    }

    [Fact]
    public async Task GetTokenAsync_RemovesOnlyOneToolAddedTrailingNewline()
    {
        var runner = new RecordingProcessRunner(_ => Success(standardOutput: "token\n\n"));
        var store = new MacOsCredentialStore(runner);

        var token = await store.GetTokenAsync();

        Assert.Equal("token\n", token);
        var request = Assert.Single(runner.Requests);
        Assert.Equal("/usr/bin/security", request.FileName);
        Assert.Equal(["find-generic-password", "-a", "trello-token", "-s", "trello-cli", "-w"], request.Arguments);
        Assert.Null(request.StandardInput);
    }

    [Fact]
    public async Task DeleteTokenAsync_TreatsANotFoundCredentialAsSuccess()
    {
        var runner = new RecordingProcessRunner(_ => new ProcessRunResult(true, false, 44, "", "credential could not be found"));
        var store = new MacOsCredentialStore(runner);

        await store.DeleteTokenAsync();

        var request = Assert.Single(runner.Requests);
        Assert.Equal(["delete-generic-password", "-a", "trello-token", "-s", "trello-cli"], request.Arguments);
    }

    [Fact]
    public async Task GetTokenAsync_ReportsUnavailableToolsWithoutLeakingProcessOutput()
    {
        const string secret = "mac-token-canary";
        var runner = new RecordingProcessRunner(_ => new ProcessRunResult(false, false, -1, secret, $"detail-{secret}"));
        var store = new MacOsCredentialStore(runner);

        var exception = await Assert.ThrowsAsync<CredentialStoreException>(() => store.GetTokenAsync());

        Assert.Equal(CredentialStoreErrorCategory.StoreUnavailable, exception.Category);
        Assert.DoesNotContain(secret, exception.Message);
        Assert.DoesNotContain("detail-", exception.Message);
    }

    [Fact]
    public async Task SetTokenAsync_ReportsTimeoutWithoutLeakingTheToken()
    {
        const string token = "mac-token-canary";
        var runner = new RecordingProcessRunner(_ => new ProcessRunResult(true, true, -1, "", "timeout detail"));
        var store = new MacOsCredentialStore(runner);

        var exception = await Assert.ThrowsAsync<CredentialStoreException>(() => store.SetTokenAsync(token));

        Assert.Equal(CredentialStoreErrorCategory.OperationFailed, exception.Category);
        Assert.DoesNotContain(token, exception.Message);
        Assert.DoesNotContain("timeout detail", exception.Message);
    }

    private static ProcessRunResult Success(string standardOutput = "") =>
        new(true, false, 0, standardOutput, "");
}
