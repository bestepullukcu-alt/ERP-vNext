using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Domain.Repositories;

public interface IPersonReferenceRepository
{
    Task<PersonReference?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PersonReference>> SearchAsync(
        string? query,
        PersonReferenceStatus? status,
        int skip,
        int take,
        CancellationToken ct = default);
    Task<IReadOnlyList<PersonReference>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
