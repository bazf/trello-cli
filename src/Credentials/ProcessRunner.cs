using System.ComponentModel;
using System.Diagnostics;

namespace TrelloCli.Credentials;

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TerminationGrace = TimeSpan.FromMilliseconds(250);
    private readonly TimeSpan _timeout;
    private readonly IProcessLifecycle _processLifecycle;

    public ProcessRunner() : this(DefaultTimeout, SystemProcessLifecycle.Instance)
    {
    }

    public ProcessRunner(TimeSpan timeout) : this(timeout, SystemProcessLifecycle.Instance)
    {
    }

    internal ProcessRunner(TimeSpan timeout, IProcessLifecycle processLifecycle)
    {
        _timeout = timeout;
        _processLifecycle = processLifecycle;
    }

    public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new ProcessRunResult(false, false, -1, string.Empty, string.Empty);
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = new CancellationTokenSource(_timeout);
            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            var standardInput = WriteStandardInputAsync(
                process.StandardInput,
                request.StandardInput,
                operationCancellation.Token);

            try
            {
                await standardInput.WaitAsync(operationCancellation.Token);
                await process.WaitForExitAsync(operationCancellation.Token);
                await Task.WhenAll(standardOutput, standardError).WaitAsync(operationCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                await TeardownAsync(process, standardInput, standardOutput, standardError);
                return new ProcessRunResult(true, true, -1, string.Empty, string.Empty);
            }
            catch (OperationCanceledException)
            {
                await TeardownAsync(process, standardInput, standardOutput, standardError);
                throw;
            }

            return new ProcessRunResult(true, false, process.ExitCode, standardOutput.Result, standardError.Result);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new ProcessRunResult(false, false, -1, string.Empty, string.Empty);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProcessRunResult(true, false, -1, string.Empty, string.Empty);
        }
    }

    private static async Task WriteStandardInputAsync(
        StreamWriter standardInput,
        string? value,
        CancellationToken cancellationToken)
    {
        if (value is not null)
        {
            await standardInput.WriteAsync(value.AsMemory(), cancellationToken);
        }

        await standardInput.FlushAsync(cancellationToken);
        standardInput.Close();
    }

    private async Task TeardownAsync(Process process, params Task[] pendingIo)
    {
        Task? lifecycleWait = null;
        try
        {
            try
            {
                if (!process.HasExited) _processLifecycle.Kill(process);
            }
            catch (Exception)
            {
            }

            using var terminationCancellation = new CancellationTokenSource(TerminationGrace);
            try
            {
                lifecycleWait = _processLifecycle.WaitForExitAsync(process, terminationCancellation.Token);
                await lifecycleWait.WaitAsync(terminationCancellation.Token);
            }
            catch (Exception)
            {
            }
        }
        finally
        {
            StopPendingIo(process, pendingIo);
            if (lifecycleWait is not null) Observe(lifecycleWait);
        }
    }

    private static void StopPendingIo(Process process, params Task[] pendingTasks)
    {
        TryDispose(() => process.StandardInput.BaseStream);
        TryDispose(() => process.StandardOutput.BaseStream);
        TryDispose(() => process.StandardError.BaseStream);

        foreach (var pendingTask in pendingTasks)
        {
            Observe(pendingTask);
        }
    }

    private static void TryDispose(Func<IDisposable> getDisposable)
    {
        try
        {
            getDisposable().Dispose();
        }
        catch (Exception)
        {
        }
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}

internal interface IProcessLifecycle
{
    void Kill(Process process);
    Task WaitForExitAsync(Process process, CancellationToken cancellationToken);
}

internal sealed class SystemProcessLifecycle : IProcessLifecycle
{
    public static SystemProcessLifecycle Instance { get; } = new();

    public void Kill(Process process) => process.Kill(entireProcessTree: true);

    public Task WaitForExitAsync(Process process, CancellationToken cancellationToken) =>
        process.WaitForExitAsync(cancellationToken);
}
