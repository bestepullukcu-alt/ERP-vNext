using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Sourcing;

// ── Contract version sabiti (SOURCING owned contract; sourcing.openapi.yaml) ──
public static class SourcingContract
{
    public const string Version = "v1";
}

// ══ READ DTOs (sourcing.openapi.yaml şekilleriyle uyumlu) ═════════════════════

/// <summary>RfxLine read DTO — contract RfxLine {itemId, quantity, uomId}.</summary>
public sealed record RfxLineDto(
    string ItemId,
    string Quantity,
    string UomId);

/// <summary>RfxEvent read DTO — contract RfxEvent. (Id/Version house-style zarf alanı; concurrency için.)</summary>
public sealed record RfxEventDto(
    Guid Id,
    string RfxId,
    RfxType Type,
    string Title,
    RfxStatus Status,
    DateTimeOffset? ClosesAt,
    IReadOnlyList<string> InvitedSupplierIds,
    IReadOnlyList<RfxLineDto> Lines,
    int Version,
    string ContractVersion);

/// <summary>Sayfalı RFx liste sonucu — contract listRfxEvents {items, nextCursor, contractVersion}.</summary>
public sealed record RfxEventListResultDto(
    IReadOnlyList<RfxEventDto> Items,
    string? NextCursor,
    string ContractVersion);

/// <summary>BidLine read DTO — contract BidLine {itemId, unitPrice, leadTimeDays}.</summary>
public sealed record BidLineDto(
    string ItemId,
    string UnitPrice,
    int? LeadTimeDays);

/// <summary>Bid read DTO — contract Bid.</summary>
public sealed record BidDto(
    string BidId,
    string RfxId,
    string SupplierId,
    IReadOnlyList<BidLineDto> Lines,
    string? EvaluationScore,
    string ContractVersion);

/// <summary>Bid liste sonucu — contract listBids {items, contractVersion}.</summary>
public sealed record BidListResultDto(
    IReadOnlyList<BidDto> Items,
    string ContractVersion);

/// <summary>AwardDecision read DTO — contract AwardDecision.</summary>
public sealed record AwardDecisionDto(
    string RfxId,
    string AwardedBidId,
    string AwardedSupplierId,
    string? Rationale,
    DateTimeOffset DecidedAt,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract RfxUpsert / BidUpsert / award) ══

/// <summary>contract RfxLine request satırı.</summary>
public sealed record RfxLineInput(
    string ItemId,
    string Quantity,
    string UomId);

/// <summary>contract RfxUpsert request body.</summary>
public sealed record RfxUpsertRequest(
    RfxType Type,
    string Title,
    DateTimeOffset? ClosesAt,
    List<string>? InvitedSupplierIds,
    List<RfxLineInput>? Lines);

/// <summary>contract BidLine request satırı.</summary>
public sealed record BidLineInput(
    string ItemId,
    string UnitPrice,
    int? LeadTimeDays);

/// <summary>contract BidUpsert request body.</summary>
public sealed record BidUpsertRequest(
    string SupplierId,
    List<BidLineInput>? Lines);

/// <summary>contract award request body ({awardedBidId, rationale?}).</summary>
public sealed record AwardRequest(
    string AwardedBidId,
    string? Rationale);
