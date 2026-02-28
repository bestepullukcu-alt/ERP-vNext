using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// SampleEntity için repository sözleşmesi.
/// Tüm implementasyonlar tenant filtresini otomatik uygular.
/// </summary>
public interface ISampleRepository
{
    Task<SampleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SampleEntity>> GetAllAsync(CancellationToken ct = default);
    Task<SampleEntity> CreateAsync(SampleEntity entity, CancellationToken ct = default);
}
