namespace Diten.CrmService.Application.Features.VisitContentSequence;

/// <summary>
/// MOD-0155 FU04 permission key (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant.
/// <para><c>crm.visit-content.preview</c> guards the READ-ONLY preview endpoint (D-SURFACE = E). Like the FU03
/// <c>crm.visit-route.preview</c> sibling it is NOT aliased onto a territory fallback: the pack (§14) puts the real key
/// on the endpoint, so the endpoint answers 403 until an operator grants the key (F-RBAC-VISIT-CONTENT) — the intended
/// fail-closed behaviour. The in-process resolver carries no RBAC of its own; the caller (FU05 engine / FU01 handler)
/// enforces its own key, exactly as the FU06B calculator does.</para>
/// </summary>
public static class VisitContentSequencePermissions
{
    public const string Preview = "crm.visit-content.preview";

    public static readonly IReadOnlyList<string> All = new[] { Preview };
}
