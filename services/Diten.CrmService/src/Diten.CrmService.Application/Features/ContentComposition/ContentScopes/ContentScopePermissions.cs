namespace Diten.CrmService.Application.Features.ContentComposition.ContentScopes;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) content-scope permission keys (PKS-001: lowercase-dotted, ≥3 segments). Definition only —
/// this file seeds nothing; the AuthService catalog + 97c5 grant are the SCMM-14 RBAC seed (SCMM-05-S1 pattern). The
/// verbatim keys the ContentScopesController <c>[HasPermission]</c> enforces.
/// </summary>
public static class ContentScopePermissions
{
    public const string Read = "crm.content-scope.read";
    public const string Manage = "crm.content-scope.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage };
}
