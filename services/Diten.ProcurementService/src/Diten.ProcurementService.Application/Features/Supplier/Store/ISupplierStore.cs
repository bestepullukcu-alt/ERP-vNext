using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Supplier.Store;

/// <summary>
/// MOD-0140 DL-3 — the supplier store the handlers use, whichever model <c>Suppliers:Model</c> selects (K-3, §8):
/// <c>TenantShared</c> (<c>supplier_masters</c> + <c>supplier_company_relationships</c>) or <c>PerLegalEntity</c> (today's
/// per-company <c>suppliers</c>; the default and the roll-back position). Every read is scoped to the caller's tenant and
/// company (server-resolved) and returns only a supplier the caller's company has a non-deleted relationship with; every
/// write runs in a Mongo transaction that enlists the running command's central-audit event (BL-501,
/// <c>RequireEnlistment</c>, §7).
/// </summary>
public interface ISupplierStore
{
    /// <summary>True under <c>TenantShared</c>: the 1.4.0 relationship endpoints exist (404 under <c>PerLegalEntity</c>) and
    /// the common fields need <c>procurement.suppliers.master.update</c> (§14).</summary>
    bool IsTenantShared { get; }

    Task<IReadOnlyList<SupplierSnapshot>> ListAsync(SupplierStatus? status, CancellationToken ct);

    /// <summary>By canonical code, else by the caller company's legacy code (§4 code resolution).</summary>
    Task<SupplierSnapshot?> FindAsync(string code, CancellationToken ct);

    /// <summary>By <c>supplierMasterId</c> (<c>Supplier.Id</c>) — eligibility by uuid (§3.1).</summary>
    Task<SupplierSnapshot?> FindByMasterIdAsync(Guid masterId, CancellationToken ct);

    /// <summary>Many codes at once (canonical or the caller's legacy codes). Key = the requested code.</summary>
    Task<IReadOnlyDictionary<string, SupplierSnapshot>> FindManyAsync(IReadOnlyList<string> codes, CancellationToken ct);

    Task<SupplierSnapshot?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    /// <summary>
    /// The code of an existing supplier with the same tax number (K-2): under <c>TenantShared</c> tenant-wide by (country,
    /// normalized tax number) — in another company too (K-6); under <c>PerLegalEntity</c> today's rule (tenant + company,
    /// trimmed tax number). Null when there is none.
    /// </summary>
    Task<string?> FindDuplicateTaxIdAsync(string? country, string taxId, Guid? excludeMasterId, CancellationToken ct);

    /// <summary>Eligible relationships (non-deleted, Active) ordered by canonical code (ordinal), codes after <paramref name="afterCode"/>.</summary>
    Task<IReadOnlyList<SupplierSnapshot>> ListEligibleAsync(string? afterCode, int take, CancellationToken ct);

    /// <summary>Creates the supplier and the caller company's relationship. A unique-index race throws <see cref="SupplierDuplicateException"/>.</summary>
    Task<SupplierSnapshot> CreateAsync(SupplierSnapshot draft, CancellationToken ct);

    /// <summary>Writes the caller company's relationship fields under the snapshot's version. Null = concurrency conflict.</summary>
    Task<SupplierSnapshot?> SaveRelationshipAsync(SupplierSnapshot snapshot, CancellationToken ct);

    /// <summary>Writes the common fields AND the relationship fields under the snapshot's versions. Null = concurrency conflict.</summary>
    Task<SupplierSnapshot?> SaveMasterAsync(SupplierSnapshot snapshot, CancellationToken ct);

    /// <summary>Soft-deletes the caller company's relationship (the tenant-wide record stays).</summary>
    Task<bool> DeleteAsync(SupplierSnapshot snapshot, CancellationToken ct);

    Task<int> DeleteManyAsync(IReadOnlyList<SupplierSnapshot> snapshots, CancellationToken ct);

    /// <summary><c>TenantShared</c> only: a supplier of the TENANT by canonical code (for "link to this company", K-6). Null under <c>PerLegalEntity</c>.</summary>
    Task<(Guid MasterId, string SupplierId)?> FindTenantSupplierAsync(string code, CancellationToken ct);

    /// <summary>
    /// <c>TenantShared</c> only: creates the caller company's relationship with the supplier. A relationship that already
    /// exists throws <see cref="SupplierRelationshipExistsException"/>; an idempotency-key race returns the winner's record.
    /// </summary>
    Task<SupplierSnapshot> LinkAsync(Guid masterId, string? idempotencyKey, CancellationToken ct);
}

/// <summary>A unique index refused the write (same tax number, or a same-key idempotency race lost).</summary>
public sealed class SupplierDuplicateException(string? existingSupplierId, bool idempotencyRace, Exception? inner = null)
    : Exception("Supplier duplicate.", inner)
{
    public string? ExistingSupplierId { get; } = existingSupplierId;
    public bool IdempotencyRace { get; } = idempotencyRace;
}

/// <summary>The caller company already has a (non-deleted) relationship with the supplier — 409 <c>RELATIONSHIP_EXISTS</c>.</summary>
public sealed class SupplierRelationshipExistsException() : Exception("Supplier relationship exists.");
