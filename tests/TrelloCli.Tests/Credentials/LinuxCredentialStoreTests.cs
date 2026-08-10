using TrelloCli.Credentials;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class LinuxCredentialStoreTests
{
    [Fact]
    public async Task SetTokenAsync_UsesInjectedIdentifiersForAnIsolatedCredential()
    {
        var runner = new RecordingProcessRunner(_ => Success());
        var store = new LinuxCredentialStore(runner, "trello-cli-smoke-123", "trello-token-smoke-123");

        await store.SetTokenAsync("linux-token-canary");

        Assert.Equal(
            ["store", "--label=Trello CLI token", "service", "trello-cli-smoke-123", "account", "trello-token-smoke-123"],
            Assert.Single(runner.Requests).Arguments);
    }

    [Fact]
    public async Task SetTokenAsync_UsesTheExactSecretToolCommandAndWritesTheExactTokenWithoutANewline()
    {
        const string token = "linux-token-canary";
        var runner = new RecordingProcessRunner(_ => Success());
        var store = new LinuxCredentialStore(runner);

        await store.SetTokenAsync(token);

        var request = Assert.Single(runner.Requests);
        Assert.Equal("/usr/bin/secret-tool", request.FileName);
        Assert.Equal(["store", "--label=Trello CLI token", "service", "trello-cli", "account", "trello-token"], request.Arguments);
        Assert.Equal(token, request.StandardInput);
        Assert.DoesNotContain(token, request.Arguments);
    }

    [Fact]
    public async Task GetTokenAsync_UsesTheExactLookupCommandAndRemovesOnlyOneTrailingNewline()
    {
        var runner = new RecordingProcessRunner(_ => Success(standardOutput: "token\n\n"));
        var store = new LinuxCredentialStore(runner);

        var token = await store.GetTokenAsync();

        Assert.Equal("token\n", token);
        var request = Assert.Single(runner.Requests);
        Assert.Equal("/usr/bin/secret-tool", request.FileName);
        Assert.Equal(["lookup", "service", "trello-cli", "account", "trello-token"], request.Arguments);
        Assert.Null(request.StandardInput);
    }

    [Fact]
    public async Task DeleteTokenAsync_TreatsANotFoundCredentialAsSuccess()
    {
        var runner = new RecordingProcessRunner(_ => new ProcessRunResult(true, false, 1, "", "secret not found"));
        var store = new LinuxCredentialStore(runner);

        await store.DeleteTokenAsync();

        var request = Assert.Single(runner.Requests);
        Assert.Equal(["clear", "service", "trello-cli", "account", "trello-token"], request.Arguments);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNullForANotFoundCredential()
    {
        var runner = new RecordingProcessRunner(_ => new ProcessRunResult(true, false, 1, "", "secret not found"));
        var store = new LinuxCredentialStore(runner);

        var token = await store.GetTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNullWhenSecretToolLookupHasNoValueAndNoStandardError()
    {
        var runner = new RecordingProcessRunner(_ => new ProcessRunResult(true, false, 1, "", ""));
        var store = new LinuxCredentialStore(runner);

        var token = await store.GetTokenAsync();

        Assert.Null(token);
    }

    private static ProcessRunResult Success(string standardOutput = "") =>
        new(true, false, 0, standardOutput, "");
}
