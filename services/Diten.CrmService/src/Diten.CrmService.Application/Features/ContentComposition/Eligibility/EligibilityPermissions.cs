namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>
/// SCMM-11 (CAND-CAP-0011) permission keys (PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds
/// NOTHING (no DB write, no grant). The canonical CAND-CAP-0011 eligibility keys; the HTTP surface + RBAC seed/grant are
/// the SCMM-11 follow (same S1 pattern the concept keys followed). Until then the policy CRUD + evaluator are exposed
/// only through MediatR + the in-process port, so no endpoint runs on an unseeded key.
/// </summary>
public static class EligibilityPermissions
{
    public const string Read = "crm.eligibility.read";
    public const string Manage = "crm.eligibility.manage";
    public const string Evaluate = "crm.eligibility.evaluate";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Evaluate };
}
