using System.Text;

namespace Diten.BuildingBlocks.Email;

/// <summary>
/// BL-454 — makes a value safe to put in an e-mail HEADER (sender name, reply-to name, subject).
///
/// <para>A header ends at CR/LF. A tenant name, a task title or a meeting title is typed by a user, and a value
/// carrying a line break would otherwise start a header of the writer's choosing (<c>Bcc:</c>, a second
/// <c>Subject:</c>). The mail libraries encode or reject some of this on their own; this does not rely on which —
/// every control character becomes a space BEFORE the value reaches a library.</para>
/// </summary>
public static class EmailHeaderText
{
    /// <summary>The longest sender name composed. Long enough for a real company name plus the "via" part.</summary>
    public const int MaxDisplayNameLength = 120;

    /// <summary>RFC 5322 caps a line at 998 characters; a subject far below that is still a subject.</summary>
    public const int MaxSubjectLength = 250;

    public static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var ch in value)
        {
            // Control characters (CR, LF, TAB, NUL, …) and the Unicode line/paragraph separators all end or bend a
            // header line somewhere; each becomes one space, and runs of spaces collapse.
            var isBreak = char.IsControl(ch) || ch == (char)0x2028 || ch == (char)0x2029;
            var c = isBreak ? ' ' : ch;
            if (c == ' ')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            builder.Append(c);
        }

        var cleaned = builder.ToString().Trim();
        if (maxLength > 0 && cleaned.Length > maxLength)
        {
            cleaned = cleaned[..maxLength].TrimEnd();
            // Never leave half of a surrogate pair at the cut.
            if (cleaned.Length > 0 && char.IsHighSurrogate(cleaned[^1]))
            {
                cleaned = cleaned[..^1];
            }
        }

        return cleaned;
    }

    public static string CleanDisplayName(string? value) => Clean(value, MaxDisplayNameLength);

    public static string CleanSubject(string? value) => Clean(value, MaxSubjectLength);
}
