using TrelloCli.Credentials;

namespace TrelloCli.Tests.Credentials;

internal sealed class RecordingProcessRunner(Func<ProcessRunRequest, ProcessRunResult> resultFactory) : IProcessRunner
{
    public List<ProcessRunRequest> Requests { get; } = [];

    public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(resultFactory(request));
    }
}
