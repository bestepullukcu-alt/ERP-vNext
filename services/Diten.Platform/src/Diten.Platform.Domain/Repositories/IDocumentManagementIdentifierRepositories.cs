using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU07 — document identifier (Permanent UID / Document Code) allocation ledger + sequence counter contracts.
// Tenant-scoped; the ledger is never hard-deleted and its uniqueness check deliberately INCLUDES soft-deleted rows.

/// <summary>MOD-0029-FU07 — tenant-scoped list filter for the allocation ledger.</summary>
public sealed record IdentifierAllocationListFilter(
    DocumentIdentifierType? IdentifierType = null,
    DocumentIdentifierAllocationStatus? AllocationStatus = null,
    Guid? RegisterEntryId = null);

public interface IDocumentIdentifierAllocationRepository
{
    Task<DocumentIdentifierAllocation> CreateAsync(DocumentIdentifierAllocation allocation, CancellationToken ct = default);
    Task<DocumentIdentifierAllocation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// NEVER-REUSE guard: returns true if the value has EVER been allocated for this tenant+type, regardless of
    /// allocation status or soft-delete. This intentionally ignores the IsDeleted filter (SOP §6.3).
    /// </summary>
    Task<bool> ExistsValueIncludingDeletedAsync(DocumentIdentifierType type, string identifierValue, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentIdentifierAllocation>> ListAsync(IdentifierAllocationListFilter filter, CancellationToken ct = default);

    Task<bool> UpdateAsync(DocumentIdentifierAllocation allocation, CancellationToken ct = default);
}

public interface IDocumentIdentifierSequenceCounterRepository
{
    /// <summary>
    /// Atomically increments (and lazily creates) the counter for the given key and returns the newly allocated
    /// number. Monotonic and concurrency-safe; never rolls back.
    /// </summary>
    Task<long> NextAsync(DocumentIdentifierType type, string? prefix, string? domainCode, string? typeCode, string createdBy, CancellationToken ct = default);
}
