using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Application.Features.Loads;
public interface ILoadReferenceReader { Task<string> ObserveAsync(LoadPlan load, Guid? existingId, CancellationToken ct); }
