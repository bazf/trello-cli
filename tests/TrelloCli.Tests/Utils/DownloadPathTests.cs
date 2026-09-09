using TrelloCli.Utils;
using Xunit;

namespace TrelloCli.Tests.Utils;

/// <summary>
/// Attachment file names come from whoever uploaded the file, so they are untrusted input.
/// </summary>
public class DownloadPathTests
{
    private const string Fallback = "attachment-abc123";

    [Theory]
    [InlineData("../../.ssh/authorized_keys", "authorized_keys")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\config", "config")]
    [InlineData("C:\\Windows\\system.ini", "system.ini")]
    [InlineData("a/b/c.png", "c.png")]
    [InlineData("report.pdf", "report.pdf")]
    public void SanitizeFileName_KeepsOnlyTheFinalSegment(string candidate, string expected) =>
        Assert.Equal(expected, DownloadPaths.SanitizeFileName(candidate, Fallback));

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("../")]
    [InlineData("...")]
    public void SanitizeFileName_FallsBackWhenNothingUsableRemains(string? candidate) =>
        Assert.Equal(Fallback, DownloadPaths.SanitizeFileName(candidate, Fallback));

    [Theory]
    [InlineData("con")]
    [InlineData("CON")]
    [InlineData("COM1.txt")]
    [InlineData("nul.log")]
    public void SanitizeFileName_EscapesWindowsReservedDeviceNames(string candidate)
    {
        var result = DownloadPaths.SanitizeFileName(candidate, Fallback);

        Assert.StartsWith("_", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plan:v2?.txt")]
    [InlineData("we<i>rd\"name|.txt")]
    [InlineData("null\u0000byte.png")]
    public void SanitizeFileName_ReplacesCharactersThatAreIllegalOnAnyPlatform(string candidate)
    {
        var result = DownloadPaths.SanitizeFileName(candidate, Fallback);

        Assert.DoesNotContain(result, character => character is ':' or '?' or '*' or '"' or '<' or '>' or '|');
        Assert.DoesNotContain(result, char.IsControl);
    }

    [Fact]
    public void SanitizeFileName_DropsTrailingDotsAndSpacesThatWindowsRejects() =>
        Assert.Equal("report", DownloadPaths.SanitizeFileName("report. . ", Fallback));

    [Fact]
    public void SanitizeFileName_TruncatesLongNamesButKeepsTheExtension()
    {
        var result = DownloadPaths.SanitizeFileName(new string('a', 300) + ".pdf", Fallback);

        Assert.True(result.Length <= 120);
        Assert.EndsWith(".pdf", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../../.ssh/authorized_keys")]
    [InlineData("..\\..\\evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("....//....//evil")]
    public void SanitizedNames_AlwaysResolveInsideTheTargetDirectory(string candidate)
    {
        var root = Path.Combine(Path.GetTempPath(), $"trello-paths-{Guid.NewGuid():N}");
        var safe = DownloadPaths.SanitizeFileName(candidate, Fallback);

        var resolved = DownloadPaths.ResolveWithin(root, safe);

        Assert.NotNull(resolved);
        Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveWithin_RefusesAnEscapingSegmentEvenIfSanitizingIsBypassed()
    {
        var root = Path.Combine(Path.GetTempPath(), $"trello-paths-{Guid.NewGuid():N}");

        Assert.Null(DownloadPaths.ResolveWithin(root, ".." + Path.DirectorySeparatorChar + "escaped.txt"));
    }

    [Fact]
    public void ClaimUniquePath_NumbersNamesAlreadyClaimedInThisRun()
    {
        var reserved = DownloadPaths.NewReservationSet();
        var path = Path.Combine(Path.GetTempPath(), $"trello-paths-{Guid.NewGuid():N}", "report.pdf");

        Assert.Equal(path, DownloadPaths.ClaimUniquePath(path, reserved, overwriteExisting: false));
        Assert.Equal(
            Path.Combine(Path.GetDirectoryName(path)!, "report-2.pdf"),
            DownloadPaths.ClaimUniquePath(path, reserved, overwriteExisting: false));
    }

    [Fact]
    public void ClaimUniquePath_StillNumbersWithinARunWhenOverwritingIsAllowed()
    {
        // --overwrite means "replace what was already on disk", not "let two attachments
        // collapse into one file".
        var reserved = DownloadPaths.NewReservationSet();
        var path = Path.Combine(Path.GetTempPath(), $"trello-paths-{Guid.NewGuid():N}", "report.pdf");

        DownloadPaths.ClaimUniquePath(path, reserved, overwriteExisting: true);

        Assert.Equal(
            Path.Combine(Path.GetDirectoryName(path)!, "report-2.pdf"),
            DownloadPaths.ClaimUniquePath(path, reserved, overwriteExisting: true));
    }
}
