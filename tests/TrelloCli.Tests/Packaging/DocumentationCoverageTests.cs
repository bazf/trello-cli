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
    private static readonly string[] Surfaces =
    [
        "README.md",
        "docs/instruction.md",
        "docs/system-prompt.md",
        "plugins/trello-cli/skills/trello-cli/SKILL.md",
        "plugins/trello-cli/skills/trello-cli/REFERENCE.md"
    ];

    public static TheoryData<string> DocumentedSurfaces => [.. Surfaces];

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

    /// <summary>
    /// Attachment download was documented as impossible for years on the grounds that Trello's
    /// download endpoint "requires browser authentication". It does not: it requires the same
    /// Authorization header every other call here already sends. The claim was repeated across
    /// the catalog and all five documents, so guard against it coming back.
    /// </summary>
    [Fact]
    public void NoDocumentClaimsAttachmentDownloadIsUnsupported()
    {
        string[] falseClaims =
        [
            "requires browser authentication",
            "requires browser session authentication",
            "Downloading attachments is not supported",
            "Downloading attachment content is not supported"
        ];

        var sources = Surfaces.Append("src/Cli/CommandCatalog.cs");

        foreach (var source in sources)
        {
            var text = ReadRepositoryFile(source);
            foreach (var claim in falseClaims)
                Assert.DoesNotContain(claim, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..", relativePath)));
}
