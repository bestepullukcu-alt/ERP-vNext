using System.Globalization;

namespace Diten.CrmService.Application.Common.ReferenceValidation;

/// <summary>
/// Parses the per-value <c>attributes</c> metadata of a MOD-0048 published value. The consumer seam
/// (<see cref="IReferenceMetadataReader"/>) exposes attributes as a <c>Dictionary&lt;string,string&gt;</c>
/// (MOD-0048 returns them as strings even for numeric/boolean values), so metadata is ALWAYS parsed from strings —
/// never read as a native JSON number/bool. A missing key or an unparseable value is a controlled failure for the
/// caller (fail-closed); it is never silently defaulted.
/// </summary>
public static class ReferenceMetadata
{
    public static bool TryGetInt(IReadOnlyDictionary<string, string>? attributes, string key, out int value)
    {
        value = 0;
        if (attributes is null || !attributes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryGetBool(IReadOnlyDictionary<string, string>? attributes, string key, out bool value)
    {
        value = false;
        if (attributes is null || !attributes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return bool.TryParse(raw.Trim(), out value);
    }
}
