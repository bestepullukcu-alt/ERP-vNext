namespace Diten.CrmService.Application.Features.Territory.ResourceAssignments;

/// <summary>
/// FU04A compatibility seam until the canonical Position Directory exposes territory metadata.
/// This table is position based; the retired territory-resource-role vocabulary is never consulted.
/// Unknown, complete snapshots are planning-only and therefore fail closed during operational activation.
/// <para>WP-VW-W3L (K-L7) — a country position code is the same position: a code EQUAL to a rule key, or ending with
/// "-" + the key (<c>uz-medical-representative</c>, <c>tr-area-manager</c>), takes that key's rule; the longest key wins
/// when several fit. Anything else (<c>medical-representative-x</c>, <c>xmedical-representative</c>) stays unknown.</para>
/// </summary>
public static class TerritoryPositionPolicy
{
    public const string BuiltInSource = "fu04a-deterministic-position-policy-v1";
    public const string UnverifiedSource = "position-snapshot-unverified";

    public sealed record Rule(bool TerritoryRequired, IReadOnlySet<string> AllowedLevels);

    private static readonly IReadOnlyDictionary<string, Rule> Rules =
        new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase)
        {
            ["medical-representative"] = new(true, Set("zone", "microzone")),
            ["area-manager"] = new(true, Set("area")),
            ["regional-manager"] = new(true, Set("region")),
            ["product-manager"] = new(false, Set()),
            ["hoc"] = new(false, Set()),
            ["commercial-manager"] = new(false, Set())
        };

    public static bool TryResolve(string positionCode, out Rule rule)
    {
        var code = positionCode.Trim();
        if (Rules.TryGetValue(code, out rule!))
        {
            return true;
        }

        var key = Rules.Keys
            .Where(k => code.Length > k.Length + 1 && code.EndsWith("-" + k, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(k => k.Length)
            .FirstOrDefault();
        return key is not null && Rules.TryGetValue(key, out rule!);
    }

    private static IReadOnlySet<string> Set(params string[] values)
        => values.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
