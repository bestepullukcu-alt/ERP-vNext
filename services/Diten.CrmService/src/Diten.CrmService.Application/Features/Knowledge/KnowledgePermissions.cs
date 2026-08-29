namespace Diten.CrmService.Application.Features.Knowledge;

/// <summary>
/// MOD-0162 FU02 permission keys (PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds NOTHING
/// (no DB write, no role template, no grant). The RBAC catalog does not carry <c>crm.knowledge.*</c> yet, so the
/// endpoints run on the documented fallback: <see cref="ReadFallback"/> for reads and <see cref="ManageFallback"/> for
/// writes — the same fallback MOD-0165 FU04 / MOD-0164 FU02 use. The fallback widens nothing: every FU02 guard still
/// runs behind it. RBAC alignment is deliberately left last (user decision). Follow-up: MOD-0162-FU02-RBAC.
/// </summary>
public static class KnowledgePermissions
{
    public const string Read = "crm.knowledge.read";
    public const string Manage = "crm.knowledge.manage";
    public const string SubjectRead = "crm.knowledge.subject.read";
    public const string SubjectManage = "crm.knowledge.subject.manage";

    /// <summary>Documented fallback until the FU-RBAC alignment lands (already granted to CRM roles).</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, SubjectRead, SubjectManage };
}
