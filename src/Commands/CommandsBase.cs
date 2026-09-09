using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

/// <summary>
/// Shared plumbing for the command groups: the API client they call and the writer
/// they report through. Output goes to an injected <see cref="TextWriter"/> rather
/// than straight to the console so command behaviour can be asserted in tests.
/// </summary>
public abstract class CommandsBase(TrelloApiService api, TextWriter output)
{
    protected readonly TrelloApiService Api = api;

    protected void Write<T>(T response) => output.WriteLine(OutputFormatter.ToJson(response));
}
