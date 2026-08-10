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

            if (request.StandardInput is not null)
            {
                await process.StandardInput.WriteAsync(request.StandardInput.AsMemory(), cancellationToken);
            }

            process.StandardInput.Close();

            using var timeout = new CancellationTokenSource(_timeout);
            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);

            try
            {
                await process.WaitForExitAsync(waitCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(standardOutput, standardError);
                return new ProcessRunResult(true, true, -1, standardOutput.Result, standardError.Result);
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
}
