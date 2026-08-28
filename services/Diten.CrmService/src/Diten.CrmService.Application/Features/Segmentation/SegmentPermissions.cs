namespace Diten.CrmService.Application.Features.Segmentation;

/// <summary>
/// MOD-0167 FU02 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant, no repository or collection reference.
/// <para><c>.resolve</c> is a separate key on purpose: reading a segment DEFINITION must not be enough to see the
/// IDENTITY of its members (PII). <c>.activate</c> is separate for separation of duty: whoever writes the rule need not
/// be whoever puts it live.</para>
/// <para>The RBAC catalog does not carry <c>crm.segment.*</c> yet, so the endpoints run on the same documented
/// DEV-ONLY fallback MOD-0165-FU04 / MOD-0164-FU02 / MOD-0162-FU0x already use. The fallback <b>widens no guard</b>:
/// tenant isolation, lifecycle, freeze, catalog and fail-closed rules all still run behind it. Under the fallback
/// <c>activate</c> collapses onto manage and <c>resolve</c> onto read, so the SoD and the PII split cannot be enforced
/// in dev — a deliberate, documented gap closed by follow-up F-RBAC.</para>
/// </summary>
public static class SegmentPermissions
{
    public const string Read = "crm.segment.read";
    public const string Manage = "crm.segment.manage";
    public const string Activate = "crm.segment.activate";
    public const string Resolve = "crm.segment.resolve";
    public const string TargetRead = "crm.segment.target.read";
    public const string TargetManage = "crm.segment.target.manage";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands (already granted to CRM roles). Not for production.</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Read, Manage, Activate, Resolve, TargetRead, TargetManage
    };
}
