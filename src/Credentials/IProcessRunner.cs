namespace TrelloCli.Credentials;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default);
}

public sealed record ProcessRunRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? StandardInput);

public sealed record ProcessRunResult(
    bool IsAvailable,
    bool TimedOut,
    int ExitCode,
    string StandardOutput,
    string StandardError);
