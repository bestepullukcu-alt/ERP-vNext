namespace Diten.CrmService.Application.Features.ConsentPreference;

/// <summary>
/// MOD-0164 FU02 permission keys (PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds NOTHING
/// (no DB write, no role template, no grant). The RBAC catalog does not carry <c>crm.consent.*</c> /
/// <c>crm.preference.*</c> yet, so the endpoints run on the documented fallback: <see cref="ReadFallback"/> for
/// reads/evaluate and <see cref="ManageFallback"/> for writes — the same fallback MOD-0165 FU03 uses. The fallback
/// widens nothing: every FU02 guard still runs behind it.
/// <para>
/// Least privilege note (FU01 §15): a consuming module needs <see cref="Evaluate"/> only — it never needs
/// <see cref="Read"/> on the raw consent records. SoD between <see cref="Manage"/> (authoring) and
/// <see cref="Read"/> stays available to role design. Follow-up: MOD-0164-FU-RBAC.
/// </para>
/// </summary>
public static class ConsentPreferencePermissions
{
    public const string Read = "crm.consent.read";
    public const string Manage = "crm.consent.manage";
    public const string Evaluate = "crm.consent.evaluate";
    public const string PreferenceRead = "crm.preference.read";
    public const string PreferenceManage = "crm.preference.manage";

    /// <summary>Documented fallback until the FU-RBAC alignment lands (already granted to CRM roles).</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Read, Manage, Evaluate, PreferenceRead, PreferenceManage
    };
}
