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
}
