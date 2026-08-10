using TrelloCli.Services;

namespace TrelloCli;

public sealed class TrelloCliServiceFactory(TextWriter output) : ICliServiceFactory
{
    public CliServices Create(ConfigService config)
    {
        var api = new TrelloApiService(config);
        return new CliServices(api, new CommandDispatcher(api, output));
    }
}
