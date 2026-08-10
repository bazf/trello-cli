using System.ComponentModel;
using System.Diagnostics;
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
        var lifecycle = new RecordingProcessLifecycle();
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(100), lifecycle);

        var result = await runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--wait-without-reading-stdin"], null));

        Assert.True(result.IsAvailable);
        Assert.True(result.TimedOut);
        Assert.Equal(1, lifecycle.KillCalls);
        Assert.Equal(1, lifecycle.WaitCalls);
        Assert.True(lifecycle.ExitedWhenWaitCompleted);
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

    [Fact]
    public async Task RunAsync_WhenTerminationFailsAfterTimeout_ReturnsSanitizedTimeoutWithoutHanging()
    {
        var lifecycle = new FailingKillProcessLifecycle();
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(100), lifecycle);
        var run = runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--wait-without-reading-stdin"], null));

        try
        {
            var result = await run.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.True(result.IsAvailable);
            Assert.True(result.TimedOut);
            Assert.Equal(-1, result.ExitCode);
            Assert.Empty(result.StandardOutput);
            Assert.Empty(result.StandardError);
            Assert.Equal(1, lifecycle.KillCalls);
            Assert.Equal(0, lifecycle.WaitCalls);
        }
        finally
        {
            await lifecycle.CleanupAsync();
        }
    }

    [Fact]
    public async Task RunAsync_WhenTerminationFailsAfterCallerCancellation_PropagatesCancellationWithoutHanging()
    {
        var lifecycle = new FailingKillProcessLifecycle();
        var runner = new ProcessRunner(TimeSpan.FromSeconds(10), lifecycle);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var run = runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--wait-without-reading-stdin"], null),
            cancellation.Token);

        try
        {
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => run.WaitAsync(TimeSpan.FromSeconds(1)));

            Assert.True(cancellation.IsCancellationRequested);
            Assert.True(run.IsCanceled);
            Assert.DoesNotContain(FailingKillProcessLifecycle.FailureCanary, exception.ToString());
            Assert.Equal(1, lifecycle.KillCalls);
            Assert.Equal(0, lifecycle.WaitCalls);
        }
        finally
        {
            await lifecycle.CleanupAsync();
        }
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

    private sealed class RecordingProcessLifecycle : IProcessLifecycle
    {
        public int KillCalls { get; private set; }
        public int WaitCalls { get; private set; }
        public bool ExitedWhenWaitCompleted { get; private set; }

        public void Kill(Process process)
        {
            KillCalls++;
            process.Kill(entireProcessTree: true);
        }

        public async Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
        {
            WaitCalls++;
            await process.WaitForExitAsync(cancellationToken);
            ExitedWhenWaitCompleted = process.HasExited;
        }
    }

    private sealed class FailingKillProcessLifecycle : IProcessLifecycle
    {
        public const string FailureCanary = "termination-detail-canary";
        private readonly TaskCompletionSource _waitCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int? _processId;

        public int KillCalls { get; private set; }
        public int WaitCalls { get; private set; }

        public void Kill(Process process)
        {
            KillCalls++;
            _processId = process.Id;
            throw new Win32Exception(FailureCanary);
        }

        public Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
        {
            WaitCalls++;
            return _waitCompletion.Task;
        }

        public async Task CleanupAsync()
        {
            try
            {
                if (_processId is int processId)
                {
                    using var process = Process.GetProcessById(processId);
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or Win32Exception)
            {
            }
            finally
            {
                _waitCompletion.TrySetResult();
            }
        }
    }
}
