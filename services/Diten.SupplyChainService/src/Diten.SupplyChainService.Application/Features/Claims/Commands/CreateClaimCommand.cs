using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Claims.Commands;
public sealed record CreateClaimCommand(JsonElement Body):IRequest<Response<ClaimResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.ClaimModule, SupplyChainCentralAudit.Create, "Claim");
    // The repository enlists with the id of the aggregate it wrote.
    public Guid? CentralAuditEntityId(object? response) => null;
}
