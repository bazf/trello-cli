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
        var request = new ProcessRunRequest(TestHelperExecutable, [], secret);

        var result = await runner.RunAsync(request);

        Assert.True(result.IsAvailable);
        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(secret, result.StandardOutput);
        Assert.Empty(request.Arguments);
    }

    [Fact]
    public async Task RunAsync_ReportsAnUnavailableExecutableWithoutThrowingItsRawError()
    {
        var runner = new ProcessRunner();
        var unavailableExecutable = Path.Combine(
            Path.GetTempPath(),
            $"trello-cli-missing-process-{Guid.NewGuid():N}");

        var result = await runner.RunAsync(new ProcessRunRequest(unavailableExecutable, [], null));

        Assert.False(result.IsAvailable);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_TerminatesProcessesThatExceedItsConfiguredTimeout()
    {
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(100));

        var result = await runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--wait-without-reading-stdin"], null));

        Assert.True(result.IsAvailable);
        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_TimesOutWhileWritingToAChildThatDoesNotReadStandardInput()
    {
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(100));
        using var testGuard = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await runner.RunAsync(
            new ProcessRunRequest(
                TestHelperExecutable,
                ["--wait-without-reading-stdin"],
                new string('x', 16 * 1024 * 1024)),
            testGuard.Token);

        Assert.True(result.IsAvailable);
        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    private static string TestHelperExecutable
    {
        get
        {
            var testOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            var configuration = testOutputDirectory.Parent!.Name;
            var testsDirectory = testOutputDirectory.Parent.Parent!.Parent!.Parent!.FullName;
            var executableName = OperatingSystem.IsWindows()
                ? "TrelloCli.ProcessRunnerTestHelper.exe"
                : "TrelloCli.ProcessRunnerTestHelper";

            return Path.Combine(
                testsDirectory,
                "TrelloCli.ProcessRunnerTestHelper",
                "bin",
                configuration,
                "net10.0",
                executableName);
        }
    }
}
