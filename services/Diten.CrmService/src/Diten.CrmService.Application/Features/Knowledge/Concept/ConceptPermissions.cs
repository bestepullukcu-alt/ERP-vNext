namespace Diten.CrmService.Application.Features.Knowledge.Concept;

/// <summary>
/// MOD-0162 FU03 permission keys (PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds NOTHING
/// (no DB write, no role template, no grant). The RBAC catalog does not carry <c>crm.knowledge.concept.*</c> yet, so the
/// endpoints run on the documented fallback (<see cref="ReadFallback"/> / <see cref="ManageFallback"/> — the same one
/// FU02 uses). The fallback widens nothing: every FU03 guard still runs behind it. The fallback is DEV-ONLY and must not
/// be granted to a production tenant; canonical keys land with the follow-up MOD-0162-FU03-RBAC. The Global Product
/// picker additionally consumes the MDM-owned <c>mdm.global-products.read</c> permission (granted separately by MOD-0290).
/// </summary>
public static class ConceptPermissions
{
    public const string Read = "crm.knowledge.concept.read";
    public const string Manage = "crm.knowledge.concept.manage";
    public const string TemplateManage = "crm.knowledge.concept-template.manage";
    public const string LinkManage = "crm.knowledge.concept-link.manage";

    /// <summary>Documented DEV-ONLY fallback until the FU-RBAC alignment lands (already granted to CRM roles).</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, TemplateManage, LinkManage };
}
