namespace Diten.CrmService.Domain.Repositories;

public interface IAccountCodeSequenceRepository
{
    /// <summary>Atomically increments and returns the next sequence value for (TenantId, Year).</summary>
    Task<long> NextAsync(Guid tenantId, int year, CancellationToken cancellationToken);
}
