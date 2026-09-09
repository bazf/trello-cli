using System.Text;

namespace TrelloCli.Utils;

/// <summary>
/// Turns the file name Trello reports for an attachment into something safe to write.
/// Anyone with access to a board can name a file, so the name is untrusted input:
/// it may contain path separators, characters that are illegal on another platform,
/// or a Windows reserved device name.
/// </summary>
internal static class DownloadPaths
{
    private const int MaxFileNameLength = 120;

    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // Path.GetInvalidFileNameChars() is only { '/', '\0' } on Unix, so a name containing
    // ':' or '?' would pass through there and produce a file unusable on Windows. The
    // union keeps a download portable regardless of where it ran.
    private static readonly char[] AlwaysInvalid = [':', '*', '?', '"', '<', '>', '|', '/', '\\'];

    /// <summary>Reduces an attachment file name to a single safe path segment.</summary>
    internal static string SanitizeFileName(string? candidate, string fallback)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return fallback;

        // Normalize backslashes first: on Unix they are ordinary characters, so
        // Path.GetFileName("..\\..\\etc\\passwd") returns the whole string unchanged
        // and a Windows-style traversal would survive as a literal file name.
        var name = candidate.Replace('\\', '/');
        var lastSeparator = name.LastIndexOf('/');
        if (lastSeparator >= 0) name = name[(lastSeparator + 1)..];

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            var invalid = char.IsControl(character)
                || Array.IndexOf(AlwaysInvalid, character) >= 0
                || Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0;
            builder.Append(invalid ? '_' : character);
        }

        // Windows rejects trailing dots and spaces.
        name = builder.ToString().TrimEnd('.', ' ');

        if (name.Length == 0 || name == "." || name == "..") return fallback;

        name = Truncate(name);

        var stem = Path.GetFileNameWithoutExtension(name);
        return WindowsReservedNames.Contains(stem) ? $"_{name}" : name;
    }

    private static string Truncate(string name)
    {
        if (name.Length <= MaxFileNameLength) return name;

        var extension = Path.GetExtension(name);
        if (extension.Length >= MaxFileNameLength) return name[..MaxFileNameLength];

        return name[..(MaxFileNameLength - extension.Length)] + extension;
    }

    /// <summary>
    /// Resolves <paramref name="fileName"/> inside <paramref name="directory"/>, returning null
    /// when the result would land outside it. A second, independent line of defence: sanitizing
    /// should already make this unreachable.
    /// </summary>
    internal static string? ResolveWithin(string directory, string fileName)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var full = Path.GetFullPath(Path.Combine(root, fileName));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return full.StartsWith(root + Path.DirectorySeparatorChar, comparison) ? full : null;
    }

    /// <summary>
    /// Picks a path not already taken, appending "-2", "-3" and so on before the extension.
    /// Reserved paths are compared case-insensitively on every platform because Windows and
    /// the default macOS file system both are.
    /// </summary>
    internal static string? ClaimUniquePath(string path, ISet<string> reserved, bool overwriteExisting)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var attempt = 1; attempt <= 100; attempt++)
        {
            var candidate = attempt == 1
                ? path
                : Path.Combine(directory, $"{stem}-{attempt}{extension}");

            // Within one run two attachments may share a name; de-duplicate even under
            // --overwrite, or the second download would silently replace the first.
            if (reserved.Contains(candidate)) continue;
            if (File.Exists(candidate) && !overwriteExisting) continue;

            reserved.Add(candidate);
            return candidate;
        }

        return null;
    }

    internal static ISet<string> NewReservationSet() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
