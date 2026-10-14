using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>WP-VW-W3E (W3E-BE-a, §2.2) — the institution QR codes. Tenant scoped; one ACTIVE row per institution is the
/// database's rule (partial unique index), so a concurrent second issue loses (<see cref="InsertAsync"/> answers false).</summary>
public interface IInstitutionQrCodeRepository
{
    Task<InstitutionQrCode?> GetActiveAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>The ACTIVE codes of these institutions in ONE read (the parent chain, the bulk / list pages).</summary>
    Task<IReadOnlyList<InstitutionQrCode>> ListActiveByAccountsAsync(Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);

    /// <summary>Every row of one institution (active + revoked), newest version first.</summary>
    Task<IReadOnlyList<InstitutionQrCode>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>The institutions with a code of <paramref name="status"/> (active | revoked) — the list page's QR filter.</summary>
    Task<IReadOnlyList<Guid>> ListAccountIdsByStatusAsync(Guid tenantId, string status, CancellationToken cancellationToken);

    /// <summary>The latest rows (active or revoked) of these institutions, one per institution (the list page's status).</summary>
    Task<IReadOnlyList<InstitutionQrCode>> ListLatestByAccountsAsync(Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);

    /// <summary>False when the institution already has an active code (the unique index won the race).</summary>
    Task<bool> InsertAsync(InstitutionQrCode code, CancellationToken cancellationToken);

    /// <summary>Revokes the row only while it is ACTIVE at <paramref name="expectedCodeVersion"/>; false otherwise.</summary>
    Task<bool> RevokeAsync(Guid tenantId, Guid id, int expectedCodeVersion, string? by, DateTimeOffset at, string reason, CancellationToken cancellationToken);

    /// <summary>A successful scan: <c>$inc ScanCount</c> + <c>LastScannedAt</c> in one atomic update.</summary>
    Task RecordScanAsync(Guid tenantId, Guid id, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>The successful scans of an institution's codes since <paramref name="since"/> (the visit starts that matched it).</summary>
    Task<int> CountScansSinceAsync(Guid tenantId, Guid accountId, DateTimeOffset since, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W3E (§2.2) — the append-only QR history.</summary>
public interface IInstitutionQrEventRepository
{
    Task InsertManyAsync(IReadOnlyCollection<InstitutionQrEvent> events, CancellationToken cancellationToken);

    /// <summary>One institution's events, newest first.</summary>
    Task<IReadOnlyList<InstitutionQrEvent>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);
}
