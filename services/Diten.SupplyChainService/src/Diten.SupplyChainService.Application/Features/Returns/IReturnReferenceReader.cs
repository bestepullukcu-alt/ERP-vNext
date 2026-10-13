using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns;
public interface IReturnReferenceReader { Task<ReturnReferenceSnapshot> ObserveAsync(ReturnOrder order,CancellationToken ct); }
