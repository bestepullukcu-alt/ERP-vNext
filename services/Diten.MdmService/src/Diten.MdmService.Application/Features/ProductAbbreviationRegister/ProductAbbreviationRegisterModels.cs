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
