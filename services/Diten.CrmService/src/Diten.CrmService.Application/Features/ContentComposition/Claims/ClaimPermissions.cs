namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>
/// SCMM-12 (CAND-CAP-0011) permission keys (PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds
/// NOTHING. The canonical CAND-CAP-0011 claim keys; the HTTP surface + RBAC seed/grant are the SCMM-12 follow (same S1
/// pattern). Until then claim CRUD + approval are exposed only through MediatR, so no endpoint runs on an unseeded key.
/// </summary>
public static class ClaimPermissions
{
    public const string Read = "crm.claim.read";
    public const string Manage = "crm.claim.manage";
    public const string Approve = "crm.claim.approve";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Approve };
}
