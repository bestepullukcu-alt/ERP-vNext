using Diten.BuildingBlocks.CentralAudit;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Carriers.Commands;
public sealed record CreateCarrierCommand(CreateCarrierRequest Body) : IRequest<Response<CarrierResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.CarrierModule, SupplyChainCentralAudit.Create, "Carrier");
    // The repository enlists with the id of the aggregate it wrote.
    public Guid? CentralAuditEntityId(object? response) => null;
}
