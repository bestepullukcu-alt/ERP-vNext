namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>
/// MOD-0162 FU04 permission keys (PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds NOTHING
/// (no DB write, no role template, no grant). The RBAC catalog does not carry <c>crm.knowledge.path.*</c> yet, so the
/// endpoints run on the documented DEV-ONLY fallback (<see cref="ReadFallback"/> / <see cref="ManageFallback"/> — the
/// same one FU02/FU03 use). The fallback widens nothing: every FU04 guard still runs behind it. It is DEV-ONLY and must
/// not be granted to a production tenant. Note (§14): under the fallback <c>publish</c> collapses onto <c>manage</c>, so
/// D4's separation-of-duty cannot be enforced in dev — a deliberate, documented gap closed by MOD-0162-FU04-RBAC.
/// </summary>
public static class KnowledgePathPermissions
{
    public const string Read = "crm.knowledge.path.read";
    public const string Manage = "crm.knowledge.path.manage";
    public const string Publish = "crm.knowledge.path.publish";

    /// <summary>Documented DEV-ONLY fallback until the FU-RBAC alignment lands (already granted to CRM roles).</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Publish };
}
