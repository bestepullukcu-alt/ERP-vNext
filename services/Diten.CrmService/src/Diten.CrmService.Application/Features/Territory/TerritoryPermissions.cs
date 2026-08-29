namespace Diten.CrmService.Application.Features.Territory;

/// <summary>
/// MOD-0151 permission keys used by the FU01 endpoints (pack §17, PKS-001: lowercase-dotted, ≥3 segments).
/// DEFINITION ONLY — this file seeds NOTHING (no DB write, no role template). The RBAC plan / seed is out of FU01
/// scope. The superseded keys (<c>crm.micro-zone.manage</c>, <c>crm.territory.delete</c>, and the legacy
/// <c>assign-rep</c>/<c>assign-account</c> style keys) are intentionally NOT defined here (pack D7 / §17).
/// </summary>
public static class TerritoryPermissions
{
    public const string Read = "crm.territory.read";
    public const string ModelRead = "crm.territory.model.read";
    public const string ModelManage = "crm.territory.model.manage";
    public const string NodeRead = "crm.territory.node.read";
    public const string NodeManage = "crm.territory.node.manage";

    /// <summary>MOD-0151 FU08 canonical import/export keys (pack §17). They are DEFINED here but deliberately kept
    /// out of <see cref="All"/>: the RBAC catalog does not carry them yet, so advertising them on the contract would
    /// claim a capability no role actually holds. Until the <c>FU08-RBAC</c> alignment lands, the FU08 endpoints run
    /// on the documented fallback — <see cref="ModelRead"/> for export/template and <see cref="ModelManage"/> for
    /// dry-run/apply. The fallback widens nothing: the FU05 / FU04A guards still run per row.</summary>
    public const string Export = "crm.territory.export";

    public const string Import = "crm.territory.import";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Read, ModelRead, ModelManage, NodeRead, NodeManage
    };
}
