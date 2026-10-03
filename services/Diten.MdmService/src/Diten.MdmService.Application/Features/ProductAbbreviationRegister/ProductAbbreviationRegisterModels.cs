using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister;

public static class ProductAbbreviationRegisterModels
{
    public sealed record ProductAbbreviationRegisterEntryDto(
        Guid Id,
        Guid GlobalProductId,
        string Abbreviation,
        ProductAbbreviationLifecycleStatus LifecycleStatus,
        int Version,
        Guid? ReplacesEntryId,
        bool RetirementPending);

    /// <summary>
    /// FIX1 B2 — one row of a product's register page, whatever its status. The two "by current user" flags let the
    /// page offer the requester "cancel" and everyone else "open in the Task Center" without seeing subject ids.
    /// </summary>
    public sealed record ProductAbbreviationRegisterListItemDto(
        Guid Id,
        Guid GlobalProductId,
        string Abbreviation,
        ProductAbbreviationLifecycleStatus LifecycleStatus,
        int Version,
        Guid? ReplacesEntryId,
        bool RetirementPending,
        bool CorrectionPending,
        bool RequestedByCurrentUser,
        bool RetirementRequestedByCurrentUser);

    public sealed record ProductAbbreviationAllocationResultDto(
        ProductAbbreviationRegisterEntryDto Entry,
        bool IsReplay,
        bool ReconciliationRequired);

    public sealed record ProductAbbreviationResolutionDto(
        Guid RegisterEntryId,
        Guid GlobalProductId,
        string Abbreviation,
        int Version);

    public sealed record ProductAbbreviationEvidenceItemDto(
        ProductAbbreviationHistoryEventType EventType,
        ProductAbbreviationLifecycleStatus? BeforeStatus,
        ProductAbbreviationLifecycleStatus? AfterStatus,
        string CanonicalHumanSubjectId,
        string IdempotencyKey,
        string CorrelationId,
        string? Reason,
        string EvidenceHash,
        DateTimeOffset OccurredAtUtc);

    public sealed record ProductAbbreviationAllocationEvidenceDto(
        Guid RegisterEntryId,
        Guid AllocationLedgerId,
        string Abbreviation,
        ProductAbbreviationAllocationState AllocationState,
        IReadOnlyList<ProductAbbreviationEvidenceItemDto> History);
}
