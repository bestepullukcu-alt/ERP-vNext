using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record ProductAbbreviationRegisterWriteResult(
    bool Succeeded,
    ProductAbbreviationRegisterEntry? Entry,
    string? ErrorCode = null,
    bool IsReplay = false,
    bool ReconciliationRequired = false);

public interface IProductAbbreviationRegisterRepository
{
    Task<ProductAbbreviationRegisterEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterEntry?> GetByAllocationIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterEntry?> GetActiveByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterEntry?> ResolveActiveAsync(string normalizedAbbreviation, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> InsertRequestedAsync(ProductAbbreviationRegisterEntry entry, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> TransitionAsync(
        Guid id,
        int expectedVersion,
        ProductAbbreviationLifecycleStatus expectedStatus,
        ProductAbbreviationLifecycleStatus targetStatus,
        string decisionActor,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> RequestRetirementAsync(
        Guid id,
        int expectedVersion,
        string retirementRequestId,
        string makerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> ClearRetirementRequestAsync(
        Guid id,
        int expectedVersion,
        string retirementRequestId,
        string checkerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default);
    Task<ProductAbbreviationRegisterWriteResult> ReconcileCorrectionApprovalAsync(
        Guid formerEntryId,
        int expectedFormerVersion,
        Guid replacementEntryId,
        int expectedReplacementVersion,
        string checkerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default);
}
