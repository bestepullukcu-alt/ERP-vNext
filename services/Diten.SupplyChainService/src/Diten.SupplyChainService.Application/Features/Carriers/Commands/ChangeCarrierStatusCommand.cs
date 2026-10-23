using Diten.BuildingBlocks.CentralAudit;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Carriers.Commands;
public sealed record ChangeCarrierStatusCommand(Guid CarrierId, ChangeCarrierStatusRequest Body) : IRequest<Response<CarrierResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.CarrierModule, SupplyChainCentralAudit.Transition, "Carrier");
    // The repository enlists with the id of the aggregate it wrote; the command already names it.
    public Guid? CentralAuditEntityId(object? response) => CarrierId;
}
