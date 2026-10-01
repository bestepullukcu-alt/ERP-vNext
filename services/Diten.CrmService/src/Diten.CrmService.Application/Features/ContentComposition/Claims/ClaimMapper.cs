using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>SCMM-12 (CAND-CAP-0011) aggregate ↔ DTO projection. Reads never echo TenantId (server-resolved).</summary>
public static class ClaimMapper
{
    public static ClaimDto ToDto(Claim c) => ToDto(c, null);

    /// <summary>WP-CL-BE-1 — with the list-row country summary (null when the caller has no country versions).</summary>
    public static ClaimDto ToDto(Claim c, IReadOnlyList<ClaimCountrySummaryDto>? countrySummary) => new(
        c.Id,
        c.ClaimCode,
        c.ClaimName,
        c.Description,
        c.ClaimText,
        c.Qualifiers.ToList(),
        new ClaimApplicabilityDto(
            c.Applicability.ProductRefs.ToList(),
            c.Applicability.MarketRefs.ToList(),
            c.Applicability.AudienceRefs.ToList(),
            c.Applicability.EligibilityPolicyId),
        c.EvidenceRefs.ToList(),
        c.ComponentRefs.ToList(),
        c.ClaimVersion,
        c.Status,
        c.EffectiveFrom,
        c.EffectiveTo,
        c.ApprovedAt,
        c.ApprovedBy,
        c.CreatedAt,
        c.CreatedBy,
        c.UpdatedAt,
        c.UpdatedBy,
        c.ArchivedAt,
        c.ArchivedBy,
        c.IsArchived(),
        string.IsNullOrWhiteSpace(c.Kind) ? ClaimKinds.Core : c.Kind,
        c.LocalCountryCode,
        c.ProductId,
        c.ProductDisplay,
        c.AudienceProfileIds.ToList(),
        c.ResponsibleOrgUnitId,
        c.TextLanguageCode,
        c.SupersedesClaimId,
        c.CountryClosures.Select(x => new ClaimCountryClosureDto(
            x.CountryCode, x.ReasonCode, x.ClosedBy, x.ClosedAt, x.ReopenedBy, x.ReopenedAt, x.ReopenNote,
            x.ReopenedAt is null)).ToList(),
        countrySummary ?? Array.Empty<ClaimCountrySummaryDto>());

    public static ClaimCountryVersionDto ToDto(ClaimCountryVersion v, bool isExpiring) => new(
        v.Id,
        v.ClaimCode,
        v.ClaimId,
        v.BoundCoreVersion,
        v.CountryCode,
        v.CountryVersion,
        v.Texts.Select(t => new ClaimLocalizedTextDto(t.LanguageCode, t.Text)).ToList(),
        v.Qualifiers.Select(t => new ClaimLocalizedTextDto(t.LanguageCode, t.Text)).ToList(),
        v.AdaptationTypeCode,
        v.AdaptationReason,
        v.AudienceProfileIds.ToList(),
        v.ValidFrom,
        v.ValidTo,
        v.Status,
        v.ApprovedAt,
        v.ApprovedBy,
        v.SupersedesVersionId,
        v.ReviewRounds.Count,
        isExpiring,
        v.CreatedAt,
        v.CreatedBy,
        v.UpdatedAt,
        v.UpdatedBy,
        v.ArchivedAt,
        v.ArchivedBy,
        v.IsArchived());
}
