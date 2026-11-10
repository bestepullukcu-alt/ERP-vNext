using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using SupplierEntity = Diten.ProcurementService.Domain.Entities.Supplier;

namespace Diten.ProcurementService.Application.Features.Supplier.Store;

/// <summary>
/// MOD-0140 DL-3 — <c>Suppliers:Model = PerLegalEntity</c> (the default and the roll-back position, K-3): today's per-company
/// <c>suppliers</c> record through <see cref="ISupplierRepository"/> (tenant + company filtered). One record = one relationship:
/// <see cref="SupplierSnapshot.MasterId"/> = <see cref="SupplierSnapshot.RelationshipId"/> = the record's id, no legacy codes,
/// and the 1.4.0 relationship endpoints do not exist here (404, §8). The eligibility surface is derived from the record.
/// The repository writes in a Mongo transaction that enlists the command's central-audit event (BL-501).
/// </summary>
public sealed class PerLegalEntitySupplierStore(ISupplierRepository repository) : ISupplierStore
{
    public bool IsTenantShared => false;

    public async Task<IReadOnlyList<SupplierSnapshot>> ListAsync(SupplierStatus? status, CancellationToken ct)
        => (await repository.GetAllAsync(status, ct)).Select(ToSnapshot).ToList();

    public async Task<SupplierSnapshot?> FindAsync(string code, CancellationToken ct)
        => await repository.GetBySupplierIdAsync(code, ct) is { } e ? ToSnapshot(e) : null;

    public async Task<SupplierSnapshot?> FindByMasterIdAsync(Guid masterId, CancellationToken ct)
        => await repository.GetByIdAsync(masterId, ct) is { } e ? ToSnapshot(e) : null;

    public async Task<IReadOnlyDictionary<string, SupplierSnapshot>> FindManyAsync(IReadOnlyList<string> codes, CancellationToken ct)
        => (await repository.GetBySupplierIdsAsync(codes, ct)).ToDictionary(e => e.SupplierId, ToSnapshot, StringComparer.Ordinal);

    public async Task<SupplierSnapshot?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        => await repository.GetByIdempotencyKeyAsync(idempotencyKey, ct) is { } e ? ToSnapshot(e) : null;

    /// <summary>Today's rule: same tenant + company, trimmed tax number (no country, no normalization).</summary>
    public async Task<string?> FindDuplicateTaxIdAsync(string? country, string taxId, Guid? excludeMasterId, CancellationToken ct)
        => (await repository.GetByTaxIdAsync(taxId.Trim(), excludeMasterId, ct))?.SupplierId;

    public async Task<IReadOnlyList<SupplierSnapshot>> ListEligibleAsync(string? afterCode, int take, CancellationToken ct)
        => (await repository.GetAllAsync(SupplierStatus.Active, ct))
            .Where(e => afterCode is null || string.CompareOrdinal(e.SupplierId, afterCode) > 0)
            .OrderBy(e => e.SupplierId, StringComparer.Ordinal)
            .Take(take)
            .Select(ToSnapshot)
            .ToList();

    public async Task<SupplierSnapshot> CreateAsync(SupplierSnapshot draft, CancellationToken ct)
    {
        var entity = new SupplierEntity
        {
            SupplierId = draft.SupplierId,
            Name = draft.Name,
            Status = draft.Status,
            Country = draft.Country,
            TaxId = draft.TaxId,
            Contacts = draft.Contacts,
            OnboardingStatus = draft.OnboardingStatus,
            KycOutcome = draft.KycOutcome,
            SanctionsOutcome = draft.SanctionsOutcome,
            SourceSystem = draft.SourceSystem,
            ExternalRef = draft.ExternalRef,
            IdempotencyKey = draft.IdempotencyKey
        };
        return ToSnapshot(await repository.CreateAsync(entity, ct));
    }

    public Task<SupplierSnapshot?> SaveRelationshipAsync(SupplierSnapshot snapshot, CancellationToken ct) => SaveAsync(snapshot, ct);

    public Task<SupplierSnapshot?> SaveMasterAsync(SupplierSnapshot snapshot, CancellationToken ct) => SaveAsync(snapshot, ct);

    public async Task<bool> DeleteAsync(SupplierSnapshot snapshot, CancellationToken ct) => await repository.DeleteAsync(snapshot.RelationshipId, ct);

    public async Task<int> DeleteManyAsync(IReadOnlyList<SupplierSnapshot> snapshots, CancellationToken ct)
        => snapshots.Count == 0 ? 0 : await repository.BulkDeleteAsync(snapshots.Select(s => s.RelationshipId).ToList(), ct);

    public Task<(Guid MasterId, string SupplierId)?> FindTenantSupplierAsync(string code, CancellationToken ct)
        => Task.FromResult<(Guid, string)?>(null);

    public Task<SupplierSnapshot> LinkAsync(Guid masterId, string? idempotencyKey, CancellationToken ct)
        => throw new NotSupportedException("Relationships exist only under Suppliers:Model = TenantShared.");

    /// <summary>One record: the whole snapshot is written under the record's version (the caller's If-Match is checked by the handler).</summary>
    private async Task<SupplierSnapshot?> SaveAsync(SupplierSnapshot snapshot, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(snapshot.RelationshipId, ct);
        if (entity is null || entity.Version != snapshot.MasterVersion)
        {
            return null;
        }

        entity.Name = snapshot.Name;
        entity.Country = snapshot.Country;
        entity.TaxId = snapshot.TaxId;
        entity.Contacts = snapshot.Contacts;
        entity.Documents = snapshot.Documents;
        entity.KycOutcome = snapshot.KycOutcome;
        entity.SanctionsOutcome = snapshot.SanctionsOutcome;
        entity.Status = snapshot.Status;
        entity.OnboardingStatus = snapshot.OnboardingStatus;
        entity.OnboardingCase = snapshot.OnboardingCase;
        entity.Approval = snapshot.Approval is null
            ? null
            : new OnboardingApproval
            {
                WorkflowInstanceId = snapshot.Approval.WorkflowInstanceId,
                DecidedBy = snapshot.Approval.DecidedBy,
                DecidedAt = snapshot.Approval.DecidedAt
            };
        entity.SourceSystem = snapshot.SourceSystem;
        entity.ExternalRef = snapshot.ExternalRef;
        entity.OnboardingIdempotencyKey = snapshot.OnboardingIdempotencyKey;

        return await repository.UpdateAsync(entity, snapshot.MasterVersion, ct) ? ToSnapshot(entity) : null;
    }

    internal static SupplierSnapshot ToSnapshot(SupplierEntity e) => new()
    {
        MasterId = e.Id,
        RelationshipId = e.Id,
        SupplierId = e.SupplierId,
        Name = e.Name,
        Country = e.Country,
        TaxId = e.TaxId,
        Contacts = e.Contacts,
        Documents = e.Documents,
        KycOutcome = e.KycOutcome,
        SanctionsOutcome = e.SanctionsOutcome,
        Status = e.Status,
        OnboardingStatus = e.OnboardingStatus,
        OnboardingCase = e.OnboardingCase,
        Approval = e.Approval is null
            ? null
            : new SupplierRelationshipApproval
            {
                WorkflowInstanceId = e.Approval.WorkflowInstanceId,
                DecidedBy = e.Approval.DecidedBy,
                DecidedAt = e.Approval.DecidedAt
            },
        // Today every supplier can be bought from (K-4); the per-company record has no purchasing flag of its own.
        PurchasingEnabled = true,
        PurchasingSince = e.CreatedAt,
        SourceSystem = e.SourceSystem,
        ExternalRef = e.ExternalRef,
        IdempotencyKey = e.IdempotencyKey,
        OnboardingIdempotencyKey = e.OnboardingIdempotencyKey,
        MasterVersion = e.Version,
        RelationshipVersion = 0
    };
}
