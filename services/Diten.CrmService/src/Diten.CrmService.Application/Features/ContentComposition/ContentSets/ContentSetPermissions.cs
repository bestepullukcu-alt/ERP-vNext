namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) content-set permission key (PKS-001). WP-KP-4 retired the content set: only the read of the
/// old data remains, so only the read key is enforced here. The AuthService catalog keeps every crm.content-set.* key
/// (marked deprecated) so existing role grants do not break.
/// </summary>
public static class ContentSetPermissions
{
    public const string Read = "crm.content-set.read";

    public static readonly IReadOnlyList<string> All = new[] { Read };
}
