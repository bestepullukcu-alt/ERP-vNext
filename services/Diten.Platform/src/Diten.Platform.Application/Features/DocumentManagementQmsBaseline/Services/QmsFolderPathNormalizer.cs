using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Deterministic path-segment normalization for QMS folder imports: trim, collapse internal whitespace, and
/// reject forbidden path/control characters. Normalization is pure and stable so the same source always yields the
/// same segment, full path, and (downstream) canonical id / snapshot hash.
/// </summary>
public static partial class QmsFolderPathNormalizer
{
    public const char Separator = '/';

    // Forbidden in a folder segment: path separators, and characters unsafe for a stored governance path.
    private static readonly char[] ForbiddenChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '\0'];

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static bool TryNormalizeSegment(string? raw, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "empty_or_invalid_folder_name";
            return false;
        }

        var collapsed = WhitespaceRegex().Replace(raw.Trim(), " ");

        if (collapsed.Any(c => char.IsControl(c) || ForbiddenChars.Contains(c)))
        {
            error = "forbidden_characters_in_folder_name";
            return false;
        }

        if (collapsed.Length is < 3 or > 120)
        {
            error = "folder_name_length_out_of_range";
            return false;
        }

        normalized = collapsed;
        return true;
    }

    /// <summary>
    /// Normalizes an ATOMIC folder name for dotted-outline mode: trim and collapse internal whitespace, reject only
    /// null/control characters, and enforce a generous length bound. Unlike <see cref="TryNormalizeSegment"/> this
    /// permits characters such as <c>/</c>, <c>&amp;</c>, <c>(</c>, <c>)</c> because real QMS folder names contain
    /// them (e.g. "Versioning &amp; Check-in/Check-out") and the hierarchy comes from the outline code, not the name.
    /// </summary>
    public static bool TryNormalizeAtomicName(string? raw, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "empty_or_invalid_folder_name";
            return false;
        }

        var collapsed = WhitespaceRegex().Replace(raw.Trim(), " ");

        if (collapsed.Any(char.IsControl))
        {
            error = "control_characters_in_folder_name";
            return false;
        }

        if (collapsed.Length is < 1 or > 200)
        {
            error = "folder_name_length_out_of_range";
            return false;
        }

        normalized = collapsed;
        return true;
    }

    /// <summary>Splits a slash-separated path into raw segments (empty segments dropped).</summary>
    public static IReadOnlyList<string> SplitPath(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Builds a deterministic full path from ordered normalized segments.</summary>
    public static string BuildFullPath(IEnumerable<string> normalizedSegments) =>
        string.Join(Separator, normalizedSegments);

    /// <summary>Case-insensitive key used for sibling-uniqueness and parent resolution.</summary>
    public static string CaseInsensitiveKey(string fullPath) => fullPath.ToLowerInvariant();
}
