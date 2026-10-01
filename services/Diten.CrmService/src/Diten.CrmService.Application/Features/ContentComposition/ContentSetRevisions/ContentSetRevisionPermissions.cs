namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

/// <summary>
/// SCMM-15 (CAND-CAP-0011) ContentSetRevision permission key. WP-KP-4 retired submit / review / render / release /
/// withdraw with the content set; only the read of old revisions (list / detail / rendered artifact) remains. The
/// AuthService catalog keeps crm.content-set.review | render | release | withdraw (marked deprecated) so role grants
/// do not break.
/// </summary>
public static class ContentSetRevisionPermissions
{
    public const string Read = "crm.content-set.read";
}
