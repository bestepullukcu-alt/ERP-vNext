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
            if (IsHidden(ch))
            {
                continue;
            }

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

    /// <summary>
    /// A display name, made safe WITHOUT relying on how a mail library quotes it.
    ///
    /// <para><c>"</c> and <c>\</c> are what close or escape a quoted display name: <c>System.Net.Mail</c> puts an ASCII
    /// name in quotes without escaping it, so <c>Acme" &lt;x@y.z&gt;, "</c> written into a tenant's sender name became a
    /// SECOND mailbox in the From header. Both are dropped. <c>&lt;</c> and <c>&gt;</c> are what delimit an address; a
    /// name has no use for them and keeping them only helps a name look like an address — dropped too.</para>
    ///
    /// <para><c>,</c> <c>;</c> <c>:</c> are KEPT. They are ordinary in company names ("Acme, Inc.", "Diten: Pharma")
    /// and harmless once the name cannot leave its quoted string or encoded word — which is exactly what removing the
    /// quote and the backslash guarantees.</para>
    /// </summary>
    public static string CleanDisplayName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var safe = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '"' or '\\' or '<' or '>')
            {
                continue;
            }

            safe.Append(ch);
        }

        return Clean(safe.ToString(), MaxDisplayNameLength);
    }

    /// <summary>
    /// Characters that change how a header LOOKS without being seen: the bidirectional embeddings, overrides and
    /// isolates (U+202A–U+202E, U+2066–U+2069) can make a subject or a sender read in an order other than the one it
    /// has; the zero-width space, word joiner and byte-order mark (U+200B, U+2060, U+FEFF) hide inside a word.
    ///
    /// <para>NOT here, deliberately: U+200C / U+200D (zero-width non-joiner / joiner), which shape Arabic and Persian
    /// writing correctly, and U+200E / U+200F (left-to-right / right-to-left mark), which only mark a direction and
    /// are needed where Arabic and Latin text meet.</para>
    /// </summary>
    public static bool IsHidden(char ch) =>
        ch is >= (char)0x202A and <= (char)0x202E
            or >= (char)0x2066 and <= (char)0x2069
            or (char)0x200B
            or (char)0x2060
            or (char)0xFEFF
            // Fix round 2: the soft hyphen, the Mongolian vowel separator, the invisible math operators (function
            // application, invisible times / separator / plus) and the deprecated format characters U+206A–U+206F.
            or (char)0x00AD
            or (char)0x180E
            or >= (char)0x2061 and <= (char)0x2064
            or >= (char)0x206A and <= (char)0x206F;

    public static string CleanSubject(string? value) => Clean(value, MaxSubjectLength);
}
