namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy;

/// <summary>
/// MOD-0165 FU03 permission keys (PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds NOTHING
/// (no DB write, no role template). The RBAC catalog does not carry <c>crm.visit-frequency-policy.*</c> yet, so the
/// endpoints run on the documented fallback: <see cref="ReadFallback"/> for reads/resolve and
/// <see cref="ManageFallback"/> for writes. The fallback widens nothing — every FU03 guard still runs. Follow-up:
/// MOD-0165-FU-RBAC.
/// </summary>
public static class VisitFrequencyPolicyPermissions
{
    public const string Read = "crm.visit-frequency-policy.read";
    public const string Manage = "crm.visit-frequency-policy.manage";
    public const string Resolve = "crm.visit-frequency-policy.resolve";

    /// <summary>Documented fallback until the FU-RBAC alignment lands (territory read is already granted to CRM roles).</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Resolve };
}
