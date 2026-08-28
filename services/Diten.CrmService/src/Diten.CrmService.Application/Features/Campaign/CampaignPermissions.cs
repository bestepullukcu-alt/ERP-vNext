namespace Diten.CrmService.Application.Features.Campaign;

/// <summary>
/// MOD-0165 FU04 permission keys (pack §18, PKS-001: lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds
/// NOTHING (no DB write, no role template, no grant). The RBAC catalog does not carry <c>crm.campaign.*</c> yet, so the
/// endpoints run on the documented fallback: <see cref="ReadFallback"/> for reads and <see cref="ManageFallback"/> for
/// writes — the same fallback MOD-0165 FU03 and MOD-0164 FU02 use. The fallback widens nothing: every FU04 guard still
/// runs behind it.
/// <para><see cref="Publish"/> is kept separate from <see cref="Manage"/> so role design can enforce SoD between
/// authoring a campaign and putting it live. FU04 exposes no publish endpoint, so the key is defined but unused.</para>
/// Follow-up: MOD-0165-FU-RBAC.
/// </summary>
public static class CampaignPermissions
{
    public const string Read = "crm.campaign.read";
    public const string Manage = "crm.campaign.manage";
    public const string TargetRead = "crm.campaign.target.read";
    public const string TargetManage = "crm.campaign.target.manage";
    public const string Publish = "crm.campaign.publish";

    /// <summary>Documented fallback until the FU-RBAC alignment lands (already granted to CRM roles).</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, TargetRead, TargetManage, Publish };
}
