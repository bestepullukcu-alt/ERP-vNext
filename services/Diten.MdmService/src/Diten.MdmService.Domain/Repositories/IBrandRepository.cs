using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IBrandRepository : IRepository<Brand>
{
    /// <summary>
    /// Tenant-scoped code uniqueness. Archived brands are INCLUDED on purpose: BrandCode is stable and
    /// permanently reserved (FU01 §3), so an archived code can never be reused. Keeping archived rows in the
    /// check also lets the unique index stay a plain one — no partial filter, hence no `$ne` (which crashes
    /// MongoDB index creation at startup).
    /// </summary>
    Task<bool> ExistsByCodeAsync(string brandCode, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX1 (items 1, 9) — inserts the brand and its <see cref="BrandHistoryEntry"/> together (one
    /// transaction where the server supports one). A second brand with the same code — including one written by a
    /// concurrent request after the pre-check — is <see cref="BrandWriteOutcome.DuplicateCode"/>, never an exception.
    /// </summary>
    Task<BrandWriteOutcome> CreateWithHistoryAsync(Brand brand, BrandHistoryEntry history, CancellationToken cancellationToken = default);

    /// <summary>
    /// FIX1 (items 1, 2) — writes ONLY the editable fields of <paramref name="proposed"/>, and only while the stored brand
    /// is still at <paramref name="expectedVersion"/> and not archived; the archive fields are never touched. The history
    /// entry is written with it. <see cref="BrandWriteOutcome.Stale"/> means nothing was written.
    /// </summary>
    Task<BrandWriteOutcome> UpdateEditableFieldsWithHistoryAsync(
        Brand proposed, int expectedVersion, BrandHistoryEntry history, CancellationToken cancellationToken = default);

    /// <summary>
    /// FIX1 (item 1) — sets the archive fields only, and only on a brand that is not archived yet and still at
    /// <paramref name="expectedVersion"/>; the history entry is written with it. <see cref="BrandWriteOutcome.Stale"/>
    /// means nothing was written (archived meanwhile, or changed meanwhile).
    /// </summary>
    Task<BrandWriteOutcome> ArchiveWithHistoryAsync(
        Brand archived, int expectedVersion, BrandHistoryEntry history, CancellationToken cancellationToken = default);

    /// <summary>The brand's own trail, oldest first.</summary>
    Task<IReadOnlyList<BrandHistoryEntry>> GetHistoryAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FIX1 (item 10) — another brand of this tenant (archived ones included) already carrying the external reference
    /// (<paramref name="sourceSystem"/>, <paramref name="externalId"/>); the lowest BrandCode when several do, so the
    /// answer is deterministic. Null when none.
    /// </summary>
    Task<Brand?> FindByExternalReferenceAsync(
        string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken = default);
}

/// <summary>The result of an atomic brand write.</summary>
public enum BrandWriteOutcome
{
    Written,
    DuplicateCode,
    Stale
}
