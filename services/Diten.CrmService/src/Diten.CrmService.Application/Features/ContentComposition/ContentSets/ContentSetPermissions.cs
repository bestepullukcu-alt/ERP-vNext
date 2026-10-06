namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) content-set permission keys (PKS-001: lowercase-dotted, ≥3 segments). Definition only — the
/// AuthService catalog + 97c5 grant are the SCMM-14 RBAC seed. The verbatim keys the ContentSetsController
/// <c>[HasPermission]</c> enforces. Read gates list/get; manage gates create / clone / edit / arrange / apply-eligibility
/// / archive (a single authoring capability; freeze / approval / release are later WPs with their own keys).
/// </summary>
public static class ContentSetPermissions
{
    public const string Read = "crm.content-set.read";
    public const string Manage = "crm.content-set.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage };
}
