using Diten.ProcurementService.Application.Features.Supplier.Store;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;

namespace Diten.ProcurementService.Persistence.Suppliers;

/// <summary>
/// MOD-0140 DL-3 (K-3, §8): under <c>Suppliers:Model = TenantShared</c> the other Procurement modules (MOD-0141…0145) keep
/// their code untouched (XMC-001) and keep asking <see cref="ISupplierRepository"/> for a supplier by code — this adapter
/// answers their two READ methods from the new model through the caller company's relationship (a supplier the company is
/// not related to is unknown to them, today's result). The returned record carries the REQUESTED code, so an old document's
/// legacy code still resolves (K-5). It is read-only: writes go through <see cref="ISupplierStore"/>.
/// </summary>
public sealed class TenantSharedSupplierLookup(ISupplierStore store) : ISupplierRepository
{
    public async Task<Supplier?> GetBySupplierIdAsync(string supplierId, CancellationToken cancellationToken = default)
        => await store.FindAsync(supplierId, cancellationToken) is { } s ? ToSupplier(supplierId, s) : null;

    public async Task<IReadOnlyList<Supplier>> GetBySupplierIdsAsync(IReadOnlyList<string> supplierIds, CancellationToken cancellationToken = default)
        => (await store.FindManyAsync(supplierIds, cancellationToken)).Select(p => ToSupplier(p.Key, p.Value)).ToList();

    public Task<IReadOnlyList<Supplier>> GetAllAsync(SupplierStatus? status = null, CancellationToken cancellationToken = default) => NotHere<IReadOnlyList<Supplier>>();
    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => NotHere<Supplier?>();
    public Task<Supplier?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => NotHere<Supplier?>();
    public Task<Supplier> CreateAsync(Supplier entity, CancellationToken cancellationToken = default) => NotHere<Supplier>();
    public Task<bool> UpdateAsync(Supplier entity, int expectedVersion, CancellationToken cancellationToken = default) => NotHere<bool>();
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => NotHere<bool>();
    public Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) => NotHere<int>();
    public Task<bool> ExistsByTaxIdAsync(string taxId, Guid? excludeId, CancellationToken cancellationToken = default) => NotHere<bool>();
    public Task<Supplier?> GetByTaxIdAsync(string taxId, Guid? excludeId, CancellationToken cancellationToken = default) => NotHere<Supplier?>();

    private static Task<T> NotHere<T>() =>
        throw new NotSupportedException("Under Suppliers:Model = TenantShared only the code lookups are served here; supplier writes go through ISupplierStore.");

    private static Supplier ToSupplier(string requestedCode, SupplierSnapshot s) => new()
    {
        Id = s.MasterId,
        SupplierId = requestedCode,
        Name = s.Name,
        Status = s.Status,
        Country = s.Country,
        TaxId = s.TaxId,
        Contacts = s.Contacts,
        OnboardingStatus = s.OnboardingStatus,
        KycOutcome = s.KycOutcome,
        SanctionsOutcome = s.SanctionsOutcome,
        SourceSystem = s.SourceSystem,
        ExternalRef = s.ExternalRef
    };
}
