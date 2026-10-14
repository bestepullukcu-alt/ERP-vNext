using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>WP-VW-W3 (W3-BE-b) — the objection library. Tenant scoped, soft-delete aware, no delete (retire is the
/// lifecycle end).</summary>
public interface IObjectionResponseRepository
{
    Task<ObjectionResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ObjectionResponse>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The tenant's records among <paramref name="ids"/> (any status; another tenant's id is simply absent).</summary>
    Task<IReadOnlyList<ObjectionResponse>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task InsertAsync(ObjectionResponse entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: succeeds only on <paramref name="expectedVersion"/>, then bumps the version.</summary>
    Task<bool> ReplaceAsync(ObjectionResponse entity, int expectedVersion, CancellationToken cancellationToken);
}
