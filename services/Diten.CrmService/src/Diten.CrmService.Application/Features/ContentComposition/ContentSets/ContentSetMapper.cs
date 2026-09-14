using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>SCMM-14 (CAND-CAP-0011) aggregate ↔ DTO projection. Reads never echo TenantId (server-resolved).</summary>
public static class ContentSetMapper
{
    public static ContentSetDto ToDto(ContentSet s) => new(
        s.Id,
        s.SetCode,
        s.SetName,
        s.Description,
        new ContentSetTemplateRefDto(s.Template.ConceptChainTemplateId, s.Template.ChainVersion),
        s.Scope is null ? null : new ContentSetScopeRefDto(s.Scope.ContentScopeId, s.Scope.ScopeVersion),
        s.SelectedComponents.Select(ToDto).ToList(),
        s.SelectedClaims.Select(ToDto).ToList(),
        s.DraftSchemaVersion,
        s.Status,
        s.EligibilitySnapshot is null ? null : ToDto(s.EligibilitySnapshot),
        s.Version,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy,
        s.ArchivedAt,
        s.ArchivedBy,
        s.IsArchived());

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
