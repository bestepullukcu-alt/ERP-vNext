using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Domain.Repositories;

public interface IPositionRepository
{
    Task<Position> CreateAsync(Position position, CancellationToken ct = default);
    Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(Position position, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
