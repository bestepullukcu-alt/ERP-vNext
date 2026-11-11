using Diten.InventoryService.Domain.Expiry.Enums;

namespace Diten.InventoryService.Application.Features.Expiry;

// FEFO-BUNDLE v1.0.0 wire shapes served by F1 (§2.1, §2.2, §2.5, §2.6, prepareResolution). Null members are omitted on the
// wire (Program.cs JSON options). Times are UTC "yyyy-MM-ddTHH:mm:ssZ", dates "yyyy-MM-dd".

public sealed record ExpiryUserRefDto(string UserId);

public sealed record ExpiryPolicyDto(
    string LegalEntityId,
    bool IsDefault,
    int ApproachingDays,
    int CriticalDays,
    string FefoOutboundMode,
    string BelowMinRemainingMode,
    string DateOverrideMode,
    IReadOnlyDictionary<string, int> SlaHours,
    int ExpiredOnHandThresholdDays,
    string? UpdatedAtUtc,
    ExpiryUserRefDto? UpdatedBy,
    int Version);

public sealed record ExpiryPolicyHistoryEntryDto(
    string Field,
    string? OldValue,
    string? NewValue,
    string Reason,
    ExpiryUserRefDto ChangedBy,
    string ChangedAtUtc,
    int PolicyVersion);

public sealed record ExpiryPolicyHistoryPageDto(IReadOnlyList<ExpiryPolicyHistoryEntryDto> Items, long Total, int Page, int PageSize);

public sealed record ExpiryItemRefDto(string ItemId, string SkuId, string SkuLevel);

public sealed record ExpiryLotRefDto(string LotId, string LotNumber, string? ExpiryDate, string? RetestDate);

public sealed record ExpiryWarehouseRefDto(string Id, string? Code, string? Name);

public sealed record ExpiryExceptionRefDto(
    string ExceptionId,
    string Number,
    string Type,
    string Origin,
    string Status,
    string? Resolution,
    ExpiryItemRefDto Item,
    ExpiryLotRefDto Lot,
    ExpiryWarehouseRefDto? Warehouse,
    decimal? Quantity,
    string? BaseUomId,
    string OpenedAtUtc,
    string DueAtUtc,
    bool Late,
    bool DueSoon,
    ExpiryUserRefDto? Assignee,
    int Version)
{
    /// <summary>FEFO-BUNDLE 1.2.0 (additive, F5): the decision of a resolved case — filled by previewEvidence (the Kanıt Paketi shows the
    /// resolutions and their MOD-0022 signature ids); listExceptions leaves them out.</summary>
    public string? ResolutionReason { get; init; }

    public string? ResolutionSignatureId { get; init; }

    public string? ResolvedAtUtc { get; init; }
}

public sealed record ExpiryExceptionPageDto(IReadOnlyList<ExpiryExceptionRefDto> Items, long Total, int Page, int PageSize);

public sealed record ExpirySourceMovementDto(string TransactionId, string? MovementType, string? SourceModule, string? SourceDocumentId);

public sealed record ExpiryPendingResolutionDto(string Resolution, string Reason, ExpiryUserRefDto SetBy, string SetAtUtc, string ExpiresAtUtc);

public sealed record ExpiryExceptionEventDto(
    long Seq,
    string Action,
    ExpiryUserRefDto? Actor,
    string AtUtc,
    string? Text,
    string? Resolution,
    string? SignatureId);

public sealed record ExpiryExceptionLinksDto(string? HoldId, string? ReversalTransactionId);

public sealed record ExpiryExceptionDetailDto(
    string ExceptionId,
    string Number,
    string Type,
    string Origin,
    string Status,
    string? Resolution,
    ExpiryItemRefDto Item,
    ExpiryLotRefDto Lot,
    ExpiryWarehouseRefDto? Warehouse,
    decimal? Quantity,
    string? BaseUomId,
    string OpenedAtUtc,
    string DueAtUtc,
    bool Late,
    bool DueSoon,
    ExpiryUserRefDto? Assignee,
    int Version,
    ExpiryUserRefDto? OpenedBy,
    string? Description,
    ExpirySourceMovementDto? SourceMovement,
    IReadOnlyDictionary<string, string> Facts,
    string? ResolutionReason,
    string? ResolutionSignatureId,
    string? ResolvedAtUtc,
    ExpiryUserRefDto? ResolvedBy,
    string? ClosedAtUtc,
    ExpiryUserRefDto? ClosedBy,
    ExpiryPendingResolutionDto? PendingResolution,
    IReadOnlyList<ExpiryExceptionEventDto> Events,
    ExpiryExceptionLinksDto Links);

public sealed record ExpirySubjectRefDto(string Module, string SubjectType, string SubjectId);

/// <summary>prepareResolution — what the MOD-0022 dialog needs to sign the prepared decision.</summary>
public sealed record ExpiryResolutionPreparedDto(
    string ExceptionId,
    string Resolution,
    bool SignatureRequired,
    string ActionCode,
    string MeaningCode,
    string VisualMode,
    ExpirySubjectRefDto Subject,
    string ExpiresAtUtc,
    int Version);

/// <summary>A policy save as the handler receives it (every writable member present — PUT is a full replacement).</summary>
public sealed record ExpiryPolicyInput(
    int ApproachingDays,
    int CriticalDays,
    FefoOutboundMode FefoOutboundMode,
    BelowMinRemainingMode BelowMinRemainingMode,
    DateOverrideMode DateOverrideMode,
    IReadOnlyDictionary<string, int> SlaHours,
    int ExpiredOnHandThresholdDays);

/// <summary>FEFO-BUNDLE §2.8 <c>DateOverride</c>.</summary>
public sealed record ExpiryDateOverrideDto(
    string OverrideId,
    string ItemId,
    string SkuId,
    string SkuLevel,
    string LotNumber,
    string ManufacturingDate,
    string? SuggestedExpiry,
    string? EnteredExpiry,
    string? SuggestedRetest,
    string? EnteredRetest,
    string Reason,
    bool SignatureRequired,
    string Status,
    string ExpiresAtUtc,
    int Version);
