using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

public interface IProductLifecycleHistoryRepository
{
    Task<ProductLifecycleHistory> CreateAsync(ProductLifecycleHistory entity, CancellationToken cancellationToken = default);
}
