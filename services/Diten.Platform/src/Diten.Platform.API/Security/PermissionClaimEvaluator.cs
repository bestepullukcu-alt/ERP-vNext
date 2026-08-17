using System.Security.Claims;

namespace Diten.Platform.API.Security;

/// <summary>
/// AG-STEP-011 / MOD-0018-FU14 Group A — side-effect-free permission-claim evaluation shared by the enforcement
/// filter (<see cref="HasPermissionAttribute"/>) and the future self-explain observer. It carries the canonical +
/// legacy-alias dual-read matching that previously lived inside the filter, so the two paths cannot drift.
///
/// This is a pure function: it reads only the supplied claims + the required permission (via the existing
/// <see cref="PermissionAliasResolver"/>). It performs no repository / HttpContext / external-service access and
/// mutates nothing. It never returns raw claim values, raw alias values, or a permission inventory.
/// </summary>
public static class PermissionClaimEvaluator
{
    /// <summary>
    /// Evaluates whether <paramref name="claims"/> satisfy <paramref name="requiredPermission"/> under the dual-read
    /// seam, and classifies how. <see cref="PermissionClaimEvaluation.IsSatisfied"/> is byte-for-byte equivalent to the
    /// filter's previous private check: a token satisfies the requirement iff it is in
    /// <c>PermissionAliasResolver.Expand(requiredPermission)</c> (case-insensitive). The classification only adds
    /// provenance (canonical vs legacy alias) for explain reasons; it does not change the allow/deny outcome.
    /// </summary>
    public static PermissionClaimEvaluation Evaluate(IEnumerable<Claim> claims, string requiredPermission)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(requiredPermission);

        // Canonical requirement → { canonical } ∪ aliases. A legacy/unknown requirement expands to only itself
        // (no auto-upgrade, fail-closed) — unchanged from the enforcement path.
        var acceptedKeys = PermissionAliasResolver.Expand(requiredPermission);

        var matchedCanonical = false;
        var matchedAlias = false;

        foreach (var claim in claims)
        {
            if (!IsPermissionClaim(claim.Type))
            {
                continue;
            }

            var tokens = claim.Value.Split(
                [' ', ',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var token in tokens)
            {
                if (string.Equals(token, requiredPermission, StringComparison.OrdinalIgnoreCase))
                {
                    // Canonical (required-key) match — case-insensitive, so a case-variant of the canonical key
                    // (e.g. PLATFORM.AUDIT.READ for platform.audit.read) is canonical provenance, NOT a legacy alias.
                    matchedCanonical = true;
                }
                else if (acceptedKeys.Contains(token, StringComparer.OrdinalIgnoreCase))
                {
                    // Not a case-variant of the canonical key, but an accepted key → a genuine legacy alias
                    // (a distinct string such as Modules.OrganizationUnit.Read for platform.organization-units.read).
                    matchedAlias = true;
                }
            }
        }

        // Canonical is preferred when both are present.
        if (matchedCanonical)
        {
            return new PermissionClaimEvaluation(PermissionClaimMatch.CanonicalMatch, requiredPermission, MatchedViaLegacyAlias: false);
        }

        if (matchedAlias)
        {
            return new PermissionClaimEvaluation(PermissionClaimMatch.LegacyAliasMatch, requiredPermission, MatchedViaLegacyAlias: true);
        }

        return new PermissionClaimEvaluation(PermissionClaimMatch.MissingClaim, requiredPermission, MatchedViaLegacyAlias: false);
    }

    /// <summary>
    /// Whether a claim type is a permission claim. Moved verbatim from <see cref="HasPermissionAttribute"/> so both
    /// the filter and the evaluator use the exact same set of accepted claim types.
    /// </summary>
    internal static bool IsPermissionClaim(string claimType) =>
        string.Equals(claimType, "permission", StringComparison.OrdinalIgnoreCase)
        || string.Equals(claimType, "permissions", StringComparison.OrdinalIgnoreCase)
        || claimType.EndsWith("/permission", StringComparison.OrdinalIgnoreCase)
        || claimType.EndsWith("/permissions", StringComparison.OrdinalIgnoreCase);
}

/// <summary>How a permission requirement was satisfied (or not). Bounded — carries no raw claim/alias value.</summary>
public enum PermissionClaimMatch
{
    /// <summary>No permission claim token satisfied the requirement (deny-compatible).</summary>
    MissingClaim = 0,

    /// <summary>Satisfied by the required (canonical) key, compared case-insensitively (case-variants count as canonical).</summary>
    CanonicalMatch = 1,

    /// <summary>Satisfied only via a genuine legacy alias — a distinct key string mapped to the canonical requirement.</summary>
    LegacyAliasMatch = 2,
}

/// <summary>
/// Bounded result of <see cref="PermissionClaimEvaluator.Evaluate"/>. Contains no raw claim values, no raw alias
/// values, and no permission inventory — only the classification + the required key + the alias flag.
/// </summary>
public readonly record struct PermissionClaimEvaluation(
    PermissionClaimMatch Match,
    string RequiredPermission,
    bool MatchedViaLegacyAlias)
{
    /// <summary>True iff the requirement is satisfied (canonical or legacy-alias match).</summary>
    public bool IsSatisfied => Match is PermissionClaimMatch.CanonicalMatch or PermissionClaimMatch.LegacyAliasMatch;
}
