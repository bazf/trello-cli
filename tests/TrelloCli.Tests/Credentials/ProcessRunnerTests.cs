using System.ComponentModel;
using System.Diagnostics;
using TrelloCli.Credentials;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class ProcessRunnerTests
{
    private static readonly TimeSpan AsyncGuardTimeout = TimeSpan.FromSeconds(3);

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
    public async Task RunAsync_WhenTheProcessCompletes_CapturesStandardOutputAndStandardError()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--write-output-and-error"], null));

        Assert.True(result.IsAvailable);
        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("captured-output", result.StandardOutput);
        Assert.Equal("captured-error", result.StandardError);
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
    public async Task RunAsync_WhenKillThrowsAggregateExceptionAfterTimeout_ReturnsSanitizedTimeoutWithoutHanging()
    {
        var lifecycle = new ControlledProcessLifecycle(
            new AggregateException(new Win32Exception(ControlledProcessLifecycle.FailureCanary)),
            killProcessBeforeThrowing: false);
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(100), lifecycle);
        var run = runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--wait-without-reading-stdin"], null));

        try
        {
            var result = await run.WaitAsync(AsyncGuardTimeout);

            Assert.True(result.IsAvailable);
            Assert.True(result.TimedOut);
            Assert.Equal(-1, result.ExitCode);
            Assert.Empty(result.StandardOutput);
            Assert.Empty(result.StandardError);
            Assert.Equal(1, lifecycle.KillCalls);
            Assert.Equal(1, lifecycle.WaitCalls);
            Assert.True(lifecycle.WaitCancellationToken.IsCancellationRequested);
        }
        finally
        {
            await lifecycle.CleanupAsync();
        }
    }

    [Fact]
    public async Task RunAsync_WhenKillThrowsAggregateExceptionAfterCallerCancellation_PropagatesCancellationWithoutHanging()
    {
        var lifecycle = new ControlledProcessLifecycle(
            new AggregateException(new InvalidOperationException(ControlledProcessLifecycle.FailureCanary)),
            killProcessBeforeThrowing: false);
        var runner = new ProcessRunner(TimeSpan.FromSeconds(10), lifecycle);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var run = runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--wait-without-reading-stdin"], null),
            cancellation.Token);

        try
        {
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => run.WaitAsync(AsyncGuardTimeout));

            Assert.True(cancellation.IsCancellationRequested);
            Assert.True(run.IsCanceled);
            Assert.DoesNotContain(ControlledProcessLifecycle.FailureCanary, exception.ToString());
            Assert.Equal(1, lifecycle.KillCalls);
            Assert.Equal(1, lifecycle.WaitCalls);
            Assert.True(lifecycle.WaitCancellationToken.IsCancellationRequested);
        }
        finally
        {
            await lifecycle.CleanupAsync();
        }
    }

    public static TheoryData<TimeSpan> DefaultAndInfiniteOperationTimeouts => new()
    {
        TimeSpan.FromSeconds(60),
        Timeout.InfiniteTimeSpan
    };

    [Theory]
    [MemberData(nameof(DefaultAndInfiniteOperationTimeouts))]
    public async Task RunAsync_WhenKillSucceedsButLifecycleWaitStalls_CallerCancellationUsesAShortIndependentGrace(
        TimeSpan operationTimeout)
    {
        var lifecycle = new ControlledProcessLifecycle(killException: null, killProcessBeforeThrowing: true);
        var runner = new ProcessRunner(operationTimeout, lifecycle);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var run = runner.RunAsync(
            new ProcessRunRequest(TestHelperExecutable, ["--wait-without-reading-stdin"], null),
            cancellation.Token);

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => run.WaitAsync(AsyncGuardTimeout));

            Assert.True(run.IsCanceled);
            Assert.Equal(1, lifecycle.KillCalls);
            Assert.Equal(1, lifecycle.WaitCalls);
            Assert.True(lifecycle.WaitCancellationToken.IsCancellationRequested);
        }
        finally
        {
            await lifecycle.CleanupAsync();
        }
    }

    [Fact]
    public async Task RunAsync_WhenAExitedParentLeavesADescendantHoldingOutputPipes_TimesOutWithoutDrainingForever()
    {
        var pidFile = Path.Combine(
            Path.GetTempPath(),
            $"trello-cli-held-pipe-{Guid.NewGuid():N}.pid");
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(100));
        var run = runner.RunAsync(
            new ProcessRunRequest(
                TestHelperExecutable,
                ["--exit-with-descendant-holding-output", pidFile],
                null));

        try
        {
            var result = await run.WaitAsync(AsyncGuardTimeout);

            Assert.True(result.IsAvailable);
            Assert.True(result.TimedOut);
            Assert.Equal(-1, result.ExitCode);
            Assert.Empty(result.StandardOutput);
            Assert.Empty(result.StandardError);
        }
        finally
        {
            await CleanupProcessFromPidFileAsync(pidFile);
            await ObserveAsync(run);
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

    private static async Task CleanupProcessFromPidFileAsync(string pidFile)
    {
        try
        {
            if (!File.Exists(pidFile)) return;
            var processId = int.Parse(await File.ReadAllTextAsync(pidFile));
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
        }
        finally
        {
            await DeleteFileWithRetryAsync(pidFile);
        }
    }

    private static async Task DeleteFileWithRetryAsync(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
            catch (UnauthorizedAccessException) when (attempt < 20)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
    }

    private sealed class ControlledProcessLifecycle(
        Exception? killException,
        bool killProcessBeforeThrowing) : IProcessLifecycle
    {
        public const string FailureCanary = "termination-detail-canary";
        private readonly TaskCompletionSource _waitCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int? _processId;

        public int KillCalls { get; private set; }
        public int WaitCalls { get; private set; }
        public CancellationToken WaitCancellationToken { get; private set; }

        public void Kill(Process process)
        {
            KillCalls++;
            _processId = process.Id;
            if (killProcessBeforeThrowing) process.Kill(entireProcessTree: true);
            if (killException is not null) throw killException;
        }

        public Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
        {
            WaitCalls++;
            WaitCancellationToken = cancellationToken;
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
