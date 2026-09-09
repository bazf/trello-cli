using TrelloCli;
using Xunit;

namespace TrelloCli.Tests.Packaging;

/// <summary>
/// Keeps the shipped documentation in step with the command catalog. Adding a command
/// without documenting it — the drift that hid --move-list and --bulk-move-lists from
/// every document except the built-in help — fails here.
/// </summary>
public class DocumentationCoverageTests
{
    public static TheoryData<string> DocumentedSurfaces =>
    [
        "README.md",
        "docs/instruction.md",
        "docs/system-prompt.md",
        "plugins/trello-cli/skills/trello-cli/SKILL.md",
        "plugins/trello-cli/skills/trello-cli/REFERENCE.md"
    ];

    [Theory]
    [MemberData(nameof(DocumentedSurfaces))]
    public void EveryCommandIsMentioned(string relativePath)
    {
        var text = ReadRepositoryFile(relativePath);

        var missing = CommandCatalog.Commands
            .Select(command => command.Name)
            .Where(name => !text.Contains(name, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"{relativePath} does not document: {string.Join(", ", missing)}. " +
            "Add them there, or drop them from CommandCatalog.");
    }

    [Theory]
    [MemberData(nameof(DocumentedSurfaces))]
    public void DiscoveryEntryPointsArePointedAt(string relativePath)
    {
        var text = ReadRepositoryFile(relativePath);

        Assert.Contains("--help", text, StringComparison.Ordinal);
        Assert.Contains("--commands", text, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..", relativePath)));
}
