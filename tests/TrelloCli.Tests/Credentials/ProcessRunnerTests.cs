using TrelloCli.Credentials;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class ProcessRunnerTests
{
    [Fact]
    public void ProcessRunner_AcceptsATimeoutForTestableProcessTermination()
    {
        Assert.NotNull(typeof(ProcessRunner).GetConstructor([typeof(TimeSpan)]));
    }

    [Fact]
    public async Task RunAsync_SendsStandardInputWithoutPuttingItInArguments()
    {
        const string secret = "process-token-canary";
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(new ProcessRunRequest("/bin/cat", [], secret));

        Assert.True(result.IsAvailable);
        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(secret, result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_ReportsAnUnavailableExecutableWithoutThrowingItsRawError()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(new ProcessRunRequest("/not/a/real-credential-tool", [], null));

        Assert.False(result.IsAvailable);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_TerminatesProcessesThatExceedItsConfiguredTimeout()
    {
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(20));

        var result = await runner.RunAsync(new ProcessRunRequest("/bin/sleep", ["1"], null));

        Assert.True(result.IsAvailable);
        Assert.True(result.TimedOut);
    }
}
