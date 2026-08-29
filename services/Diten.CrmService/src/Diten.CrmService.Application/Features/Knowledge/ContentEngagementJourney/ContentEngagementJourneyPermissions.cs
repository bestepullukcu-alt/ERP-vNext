namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;

/// <summary>
/// MOD-0162 FU05 permission keys (PKS-001: lowercase-dotted, hyphen-in-segment, depth ≥ 3). DEFINITION ONLY — this file
/// seeds NOTHING (no DB write, no role template, no grant). The RBAC catalog does not carry <c>crm.knowledge.*</c> yet,
/// so the endpoints run on the documented DEV-ONLY fallback (<see cref="ReadFallback"/> / <see cref="ManageFallback"/> —
/// the same one FU02/FU03/FU04 use). The fallback widens nothing: every FU05 guard still runs behind it. It is DEV-ONLY
/// and must not be granted to a production tenant. Note (§14): under the fallback <c>publish</c> collapses onto
/// <c>manage</c>, so the separation-of-duty cannot be enforced in dev — a deliberate, documented gap closed by
/// MOD-0162-FU05-RBAC (F-RBAC). There is deliberately NO stage-level key: a stage lives inside the journey document (S2).
/// </summary>
public static class ContentEngagementJourneyPermissions
{
    public const string Read = "crm.knowledge.content-engagement-journey.read";
    public const string Manage = "crm.knowledge.content-engagement-journey.manage";
    public const string Publish = "crm.knowledge.content-engagement-journey.publish";

    /// <summary>Documented DEV-ONLY fallback until the FU-RBAC alignment lands (already granted to CRM roles).</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Publish };
}
