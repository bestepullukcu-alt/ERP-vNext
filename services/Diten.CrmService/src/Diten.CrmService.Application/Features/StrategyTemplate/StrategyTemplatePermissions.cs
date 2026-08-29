namespace Diten.CrmService.Application.Features.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant, no repository or collection reference.
/// <para>There is deliberately no <c>.resolve</c>-style key here, because this FU returns no member and no member
/// count: reading a template must never imply the right to see the people inside its segments. That right stays
/// <c>crm.segment.resolve</c> in MOD-0167 FU02.</para>
/// <para><c>.activate</c> is separate for separation of duty: whoever authors the play need not be whoever puts it
/// live. The RBAC catalog does not carry <c>crm.strategy-template.*</c> yet, so the endpoints run on the same
/// documented DEV-ONLY fallback MOD-0167 FU02 / MOD-0165 FU04 / MOD-0164 FU02 already use. The fallback <b>widens no
/// guard</b>: tenant isolation, lifecycle, the binding freeze and the fail-closed reference proofs all still run behind
/// it. Under the fallback <c>activate</c> collapses onto manage, so SoD cannot be enforced in dev — a deliberate,
/// documented gap closed by follow-up F-RBAC.</para>
/// </summary>
public static class StrategyTemplatePermissions
{
    public const string Read = "crm.strategy-template.read";
    public const string Manage = "crm.strategy-template.manage";
    public const string Activate = "crm.strategy-template.activate";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands (already granted to CRM roles). Not for production.</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Activate };
}
