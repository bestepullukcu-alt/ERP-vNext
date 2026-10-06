namespace Diten.MdmService.Domain.Repositories;

/*
 * LSKU FIX2 item 1: the stuck child-creation admission count, over ALL tenants and without a tenant context (a number
 * only — no tenant data), as the GSKU identity discovery counts its manual reconciliations
 * (IFirstGskuIdentityWorkflowTenantPartitionDiscovery.CountManualReconciliationAsync). FIX1 asked a tenant-scoped
 * repository from an unscoped service scope: TenantContext threw, and the gauge never counted.
 */
public interface IChildCreationAdmissionCensus
{
    Task<long> CountStaleAsync(DateTimeOffset acquiredBeforeUtc, CancellationToken cancellationToken = default);

    // FG FIX1 item 1 (principle B): audited Draft-FG cancellations requested before the cutoff and still pending their
    // audit receipt — each keeps its parent GSKU blocked until the G4 delivery finishes it; counted, never hidden.
    Task<long> CountPendingFinishedGoodCancellationsAsync(DateTimeOffset requestedBeforeUtc,
        CancellationToken cancellationToken = default) => Task.FromResult(0L);
}
