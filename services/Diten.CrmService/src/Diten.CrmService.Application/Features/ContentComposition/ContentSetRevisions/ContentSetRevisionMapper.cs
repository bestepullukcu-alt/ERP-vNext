using Diten.CrmService.Application.Features.ContentComposition.ContentSets;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

/// <summary>SCMM-15 (CAND-CAP-0011) ContentSetRevision ↔ DTO projection. Reads never echo TenantId. The frozen snapshot
/// projects through the same pinned-reference read shapes as the SCMM-14 content set.</summary>
public static class ContentSetRevisionMapper
{
    public static ContentSetRevisionDto ToDto(ContentSetRevision r) => new(
        r.Id,
        r.RevisionCode,
        r.RevisionNumber,
        r.ContentSetId,
        r.ContentSetVersion,
        new ContentSetTemplateRefDto(r.Template.ConceptChainTemplateId, r.Template.ChainVersion),
        r.Scope is null ? null : new ContentSetScopeRefDto(r.Scope.ContentScopeId, r.Scope.ScopeVersion),
        r.SelectedComponents.Select(ToDto).ToList(),
        r.SelectedClaims.Select(ToDto).ToList(),
        r.EligibilitySnapshot is null ? null : ToDto(r.EligibilitySnapshot),
        r.ReviewStatus,
        r.Decision is null ? null : new ContentSetReviewDecisionDto(
            r.Decision.ReviewerId, r.Decision.Decision, r.Decision.Reason, r.Decision.DecidedAt),
        r.SubmittedBy,
        r.SubmittedAt,
        r.CorrelationId,
        r.Version,
        r.CreatedAt,
        r.CreatedBy,
        r.UpdatedAt,
        r.UpdatedBy,
        r.ArchivedAt,
        r.ArchivedBy,
        r.IsArchived());

    private static ContentSetComponentDto ToDto(ContentSetComponent c) => new(
        c.SelectionId, c.KnowledgeContentId, c.ContentVersion, c.LanguageCode, c.Role, ToDto(c.Arrangement));

    private static ContentSetClaimDto ToDto(ContentSetClaim c) => new(
        c.SelectionId, c.ClaimId, c.ClaimVersion, ToDto(c.Arrangement));

    private static ContentArrangementDto ToDto(ContentArrangement a) => new(a.TemplateStepId, a.BranchId, a.Position);

    private static ContentSetEligibilitySnapshotDto ToDto(ContentSetEligibilitySnapshot snap) => new(
        snap.EvaluatedAtUtc,
        snap.Items.Select(i => new ContentSetEligibilityItemDto(
            i.ItemKind, i.SelectionId, i.ItemId, i.PolicyId, i.State, i.BlockingLevel, i.Reason, i.PolicyVersion)).ToList());
}
