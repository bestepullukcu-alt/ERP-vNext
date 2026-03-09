using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

public interface ILegalEntityRepository
{
    Task<LegalEntity> CreateAsync(LegalEntity entity, CancellationToken cancellationToken = default);
    Task<LegalEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<LegalEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(LegalEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
