using System.Text;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// FIX-PERM-ACTION-SPELLING — one spelling for a permission's <c>Resource</c> and <c>Action</c> segments.
///
/// <para>
/// The catalog stores the same verb four ways. <c>[HasPermission("Platform.BusinessReferenceData.Version.PublishOverride")]</c>
/// and the seed literals behind it are PascalCase, MOD-0251 uses snake_case (<c>view_sensitive</c>), and everything
/// else is kebab. The <c>Key</c> never showed it — the constructor lowercases the key — but <c>Action</c> is stored
/// verbatim, so <c>Read</c> and <c>read</c> became two different verbs: two translations, two colours, two bars in
/// the action-distribution panel.
/// </para>
///
/// <para>
/// ⚠ SPLIT FIRST, LOWERCASE SECOND. Lowercasing first destroys the word boundary and <c>PublishOverride</c> comes
/// back as <c>publishoverride</c> — one unreadable word, which is exactly the single grey chip this fix removes.
/// The order is load-bearing and <c>PermissionSegmentNormalizerTests</c> fails if it is swapped.
/// </para>
///
/// <para>
/// ⚠ This NEVER feeds the <c>Key</c>. ADR-001 §1 froze permission keys: the key is still computed from the raw
/// constructor arguments and only the STORED segments are normalized, so
/// <c>platform.businessreferencedata.version.publishoverride</c> keeps its identity while its Action reads
/// <c>publish-override</c>. Every <c>[HasPermission]</c> attribute in the repository is untouched.
/// </para>
/// </summary>
public static class PermissionSegmentNormalizer
{
    /// <summary>
    /// Canonical spelling of a permission segment: lowercase kebab-case, dots preserved.
    /// <c>"BusinessReferenceData.Version"</c> → <c>"business-reference-data.version"</c>,
    /// <c>"lookup_validation"</c> → <c>"lookup-validation"</c>, <c>"bulk-delete"</c> → unchanged.
    /// A null/blank input returns <see cref="string.Empty"/>; anything already canonical round-trips unchanged.
    /// </summary>
    public static string Normalize(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        // Dots separate resource levels ("BusinessReferenceData.Version") and must survive; each level is
        // normalized on its own so a boundary is never invented across the separator.
        var levels = raw.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('.', levels.Select(NormalizeLevel));
    }

    private static string NormalizeLevel(string level)
    {
        var builder = new StringBuilder(level.Length + 4);

        for (var i = 0; i < level.Length; i++)
        {
            var ch = level[i];

            if (ch is '_' or '-' or ' ')
            {
                Separate(builder);
                continue;
            }

            // A word starts where an uppercase letter follows a lowercase/digit ("PublishOverride"), and also
            // where an acronym run ends in front of a word ("SKUMaster" → "sku-master"). Split BEFORE lowercasing:
            // once the string is lowercase the boundary is gone for good.
            if (char.IsUpper(ch) && i > 0)
            {
                var previous = level[i - 1];
                var nextIsLower = i + 1 < level.Length && char.IsLower(level[i + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                {
                    Separate(builder);
                }
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString().Trim('-');
    }

    // Never emit a leading or doubled separator ("__x" and "-X" both collapse to one boundary).
    private static void Separate(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '-')
        {
            builder.Append('-');
        }
    }
}
