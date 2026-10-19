using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.CompetitorProducts;

// WP-VW-W4 (W4-BE-b2, §2.6) — every competitor product request / DTO. TenantId is ABSENT from every request shape: it is
// resolved server-side and can never be supplied by a caller.

public sealed record CompetitorCountryPriceRequest(
    string MarketCode, decimal BoxPrice, string CurrencyCode, string ReimbursementStatusCode, DateTime? ValidFrom, string? Source);

/// <summary>Create / update payload (and the "complete" decision's fields). <c>ExpectedVersion</c> is the CAS on update.</summary>
public sealed record CompetitorProductWriteRequest(
    string? Name,
    IReadOnlyList<string>? Aliases,
    string? Company,
    IReadOnlyList<string>? ActiveIngredients,
    string? FormCode,
    string? Strength,
    string? AtcCode,
    IReadOnlyList<Guid>? CompetesWithGlobalProductIds,
    IReadOnlyList<Guid>? IndicationNodeIds,
    IReadOnlyList<CompetitorCountryPriceRequest>? CountryPrices,
    int? ExpectedVersion = null)
{
    public CompetitorProductFields ToFields() => new()
    {
        Name = Name ?? string.Empty,
        Aliases = Aliases?.ToList() ?? [],
        Company = Company ?? string.Empty,
        ActiveIngredients = ActiveIngredients?.ToList() ?? [],
        FormCode = FormCode,
        Strength = Strength,
        AtcCode = AtcCode,
        CompetesWithGlobalProductIds = CompetesWithGlobalProductIds?.ToList() ?? [],
        IndicationNodeIds = IndicationNodeIds?.ToList() ?? [],
        CountryPrices = (CountryPrices ?? []).Select(r => new CompetitorCountryPrice
        {
            MarketCode = r.MarketCode ?? string.Empty,
            BoxPrice = r.BoxPrice,
            CurrencyCode = r.CurrencyCode ?? string.Empty,
            ReimbursementStatusCode = r.ReimbursementStatusCode ?? string.Empty,
            ValidFrom = r.ValidFrom ?? default,
            Source = r.Source
        }).ToList()
    };
}

/// <summary>A rep's proposal from a report (name + company + our product + the report).</summary>
public sealed record CompetitorProposalRequest(string? Name, string? Company, Guid? ProductId, Guid? ReportId);

/// <summary>The decision draft: <c>complete</c> (fields) | <c>merge</c> (target + markets to move) | <c>reject</c> (reason).</summary>
public sealed record CompetitorDecisionRequest(
    string? Action,
    CompetitorProductWriteRequest? Complete,
    Guid? TargetId,
    IReadOnlyList<string>? MoveMarketCodes,
    string? Reason,
    int? ExpectedVersion = null);

/// <summary>A direct merge of two ACTIVE records (no workflow): the target survives; <c>Confirm</c> must be true.</summary>
public sealed record CompetitorMergeRequest(Guid TargetId, IReadOnlyList<string>? MoveMarketCodes, bool Confirm, int? ExpectedVersion = null);

public sealed record CompetitorReviewDecisionRequest(string? Decision, string? Comment);

public sealed record CompetitorVersionRequest(int? ExpectedVersion = null);

public sealed record CompetitorSummaryDto(Guid Id, string Name, string Company, string Status);

public sealed record CompetitorProductDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> Aliases,
    string Company,
    IReadOnlyList<string> ActiveIngredients,
    string? FormCode,
    string? Strength,
    string? AtcCode,
    IReadOnlyList<Guid> CompetesWithGlobalProductIds,
    IReadOnlyList<Guid> IndicationNodeIds,
    string Status,
    Guid? MergedIntoId,
    CompetitorSummaryDto? Survivor,
    IReadOnlyList<CompetitorCountryPrice> CountryPrices,
    IReadOnlyList<CompetitorPriceHistoryEntry> CountryPriceHistory,
    IReadOnlyList<CompetitorMergeTrace> MergedFrom,
    IReadOnlyList<CompetitorChange> ChangeLog,
    string? ProposedBy,
    DateTime? ProposedAt,
    Guid? ProposedFromReportId,
    Guid? ProposedForProductId,
    string? RejectedReason,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    int Version,
    int UsageCount,
    bool UsageAvailable)
{
    public static CompetitorProductDto From(CompetitorProduct e, CompetitorSummaryDto? survivor = null) => new(
        e.Id, e.Name, e.Aliases, e.Company, e.ActiveIngredients, e.FormCode, e.Strength, e.AtcCode,
        e.CompetesWithGlobalProductIds, e.IndicationNodeIds, e.Status, e.MergedIntoId, survivor,
        e.CountryPrices, e.CountryPriceHistory.OrderByDescending(h => h.ValidTo).ToList(), e.MergedFrom,
        e.ChangeLog.OrderByDescending(c => c.At).ToList(), e.ProposedBy, e.ProposedAt, e.ProposedFromReportId,
        e.ProposedForProductId, e.RejectedReason, e.CreatedBy, e.CreatedAt, e.UpdatedBy, e.UpdatedAt, e.Version,
        // G2 — the report usage counts come from the CRM (W4-BE-a aggregate read, joined by WEB-c); the MDM never calls the
        // CRM, so this is 0 / "coming soon" here.
        UsageCount: 0, UsageAvailable: false);
}

public sealed record CompetitorListDto(IReadOnlyList<CompetitorProductDto> Items, int Total, int Page, int PageSize);

/// <summary>The report's short-list row (lookup): an active record (or the caller's own proposal) + the market card.</summary>
public sealed record CompetitorLookupDto(
    Guid Id, string Name, string Company, IReadOnlyList<string> ActiveIngredients, string? FormCode, string? Strength,
    string Status, bool IsOwnProposal, CompetitorCountryPrice? Country);

public sealed record CompetitorSimilarDto(Guid Id, string Name, string Company, string Status, int Score, bool LikelySame);

public sealed record CompetitorUsageDto(Guid Id, int ReportCount, int DoctorCount, bool Available, string Note);

public sealed record CompetitorDecisionAbilities(bool CanEdit, bool CanSubmit, bool CanWithdraw, bool CanDecide);

public sealed record CompetitorDecisionDto(
    Guid Id,
    Guid ProposalId,
    string Action,
    CompetitorProductFields? Complete,
    Guid? TargetId,
    CompetitorSummaryDto? Target,
    IReadOnlyList<string> MoveMarketCodes,
    string? Reason,
    string Status,
    string? CreatedBy,
    DateTimeOffset CreatedAt,           // W4-FIX-d (F-7): when the decision was PREPARED (not applied)
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    string? AppliedBy,
    DateTimeOffset? AppliedAt,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    IReadOnlyList<ApprovalRoundDto> ReviewRounds,
    IReadOnlyList<ApprovalDecisionDto> Decisions,
    int Version,
    CompetitorDecisionAbilities Abilities);

/// <summary>A proposal row of the manager's "Temsilci önerileri" tab: the proposal, its similar records, its decision.</summary>
public sealed record CompetitorProposalDto(
    CompetitorProductDto Proposal,
    IReadOnlyList<CompetitorSimilarDto> Similar,
    CompetitorDecisionDto? Decision);
