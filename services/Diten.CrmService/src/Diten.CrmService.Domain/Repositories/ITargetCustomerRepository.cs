using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0167 FU02 TargetCustomer master (manual membership only). Tenant scoped, soft-delete aware, <b>no delete
/// method</b> — closing a row is the soft archive lifecycle. The resolver reads through the bulk list methods only:
/// a per-candidate read would be the N+1 the scale contract forbids.
/// </summary>
public interface ITargetCustomerRepository
{
    Task<TargetCustomer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every non-deleted manual row of one segment (archived included; the caller filters).</summary>
    Task<IReadOnlyList<TargetCustomer>> ListBySegmentAsync(
        Guid tenantId, Guid segmentId, CancellationToken cancellationToken);

    /// <summary>Reverse question: which segments has this subject been added to (or excluded from) by hand?</summary>
    Task<IReadOnlyList<TargetCustomer>> ListBySubjectAsync(
        Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken);

    Task InsertAsync(TargetCustomer entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace on (Id, TenantId, Version == expectedVersion). False means a concurrency conflict
    /// (409), never a silent overwrite.</summary>
    Task<bool> ReplaceAsync(TargetCustomer entity, int expectedVersion, CancellationToken cancellationToken);
}
