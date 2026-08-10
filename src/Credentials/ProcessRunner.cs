using System.ComponentModel;
using System.Diagnostics;

namespace TrelloCli.Credentials;

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _timeout;

    public ProcessRunner() : this(DefaultTimeout)
    {
    }

    public ProcessRunner(TimeSpan timeout)
    {
        _timeout = timeout;
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
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                await TerminateAsync(process, standardInput);
                await Task.WhenAll(standardOutput, standardError);
                return new ProcessRunResult(true, true, -1, standardOutput.Result, standardError.Result);
            }
            catch (OperationCanceledException)
            {
                await TerminateAsync(process, standardInput);
                throw;
            }

            await Task.WhenAll(standardOutput, standardError);
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

    private static async Task TerminateAsync(Process process, Task standardInput)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
        }

        await process.WaitForExitAsync(CancellationToken.None);
        process.StandardInput.Close();

        try
        {
            await standardInput;
        }
        catch
        {
        }
    }
}
