using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

public interface ILegalEntityRepository
{
    Task<LegalEntity> CreateAsync(LegalEntity entity, CancellationToken cancellationToken = default);
    Task<LegalEntity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LegalEntity>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<LegalEntity> UpdateAsync(LegalEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
