using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Regulatory;

// WP-KP-5a — the read models shared with KP-5a-UI. TenantId / actor never travel in a payload. Version = the business
// version of the key (1, 2, …); RowVersion = the concurrency token a PUT sends back as ExpectedVersion.

public sealed record RegulatoryTextDecisionDto(int RoundNo, string? By, string Outcome, string? Comment, DateTimeOffset At);

/// <summary>One version of the same key (the detail's version history).</summary>
public sealed record RegulatoryTextVersionDto(
    Guid Id, int Version, string Status, DateTimeOffset? ActivatedAt, DateTimeOffset? SupersededAt, DateTimeOffset CreatedAt);

public sealed record SafetyTextDto(
    Guid Id,
    string SafetyTextCode,
    Guid GlobalProductId,
    string? GlobalProductCodeDisplay,
    string CountryCode,
    string LanguageCode,
    int Version,
    string Status,
    bool IsActive,
    string Body,
    string? ShortBody,
    string? SourceDocumentRef,
    DateTimeOffset? SourceDate,
    string? ApprovalReference,
    bool CanEdit,
    bool CanSubmit,
    bool CanDecide,
    IReadOnlyList<RegulatoryTextDecisionDto> Decisions,
    Guid? WorkflowInstanceId,
    string ReviewLink,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? SupersededAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    int RowVersion,
    IReadOnlyList<RegulatoryTextVersionDto>? History = null);

public sealed record CountryLegalProfileDto(
    Guid Id,
    string CountryLegalProfileCode,
    string CountryCode,
    string LanguageCode,
    int Version,
    string Status,
    bool IsActive,
    string LegalFooterText,
    string? MarketingAuthorizationHolder,
    string? AdverseEventReportingText,
    string? PromotionalNotice,
    string? PageApprovalCodeFormat,
    bool CanEdit,
    bool CanSubmit,
    bool CanDecide,
    IReadOnlyList<RegulatoryTextDecisionDto> Decisions,
    Guid? WorkflowInstanceId,
    string ReviewLink,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? SupersededAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    int RowVersion,
    IReadOnlyList<RegulatoryTextVersionDto>? History = null);

public static class RegulatoryTextMapper
{
    public static SafetyTextDto ToDto(SafetyText t, RegulatoryTextAbilities can, IReadOnlyList<RegulatoryTextVersionDto>? history = null)
    {
        var round = t.CurrentRound();
        return new SafetyTextDto(
            t.Id, t.Code, t.GlobalProductId, t.GlobalProductCodeDisplay, t.CountryCode, t.LanguageCode, t.VersionNumber,
            t.Status, t.IsActive(), t.Body, t.ShortBody, t.SourceDocumentRef, t.SourceDate, t.ApprovalReference,
            can.CanEdit, can.CanSubmit, can.CanDecide, Decisions(t), round?.WorkflowInstanceId,
            RegulatoryTextKind.SafetyText.ReviewLink(t.Id), round?.SubmittedBy, round?.SubmittedAt, t.ActivatedAt,
            t.SupersededAt, t.ArchivedAt, t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy, t.Version, history);
    }

    public static CountryLegalProfileDto ToDto(
        CountryLegalProfile p, RegulatoryTextAbilities can, IReadOnlyList<RegulatoryTextVersionDto>? history = null)
    {
        var round = p.CurrentRound();
        return new CountryLegalProfileDto(
            p.Id, p.Code, p.CountryCode, p.LanguageCode, p.VersionNumber, p.Status, p.IsActive(), p.LegalFooterText,
            p.MarketingAuthorizationHolder, p.AdverseEventReportingText, p.PromotionalNotice, p.PageApprovalCodeFormat,
            can.CanEdit, can.CanSubmit, can.CanDecide, Decisions(p), round?.WorkflowInstanceId,
            RegulatoryTextKind.LegalProfile.ReviewLink(p.Id), round?.SubmittedBy, round?.SubmittedAt, p.ActivatedAt,
            p.SupersededAt, p.ArchivedAt, p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy, p.Version, history);
    }

    public static IReadOnlyList<RegulatoryTextVersionDto> History<T>(IEnumerable<T> sameKey) where T : RegulatoryText
        => sameKey.OrderByDescending(r => r.VersionNumber)
            .Select(r => new RegulatoryTextVersionDto(r.Id, r.VersionNumber, r.Status, r.ActivatedAt, r.SupersededAt, r.CreatedAt))
            .ToList();

    private static IReadOnlyList<RegulatoryTextDecisionDto> Decisions(RegulatoryText t)
        => t.Decisions.OrderBy(d => d.RoundNo)
            .Select(d => new RegulatoryTextDecisionDto(d.RoundNo, d.By, d.Outcome, d.Comment, d.At)).ToList();
}
