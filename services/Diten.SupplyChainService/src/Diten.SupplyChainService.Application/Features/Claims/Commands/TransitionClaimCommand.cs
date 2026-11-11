using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Claims.Commands;
public sealed record TransitionClaimCommand(Guid ClaimId,JsonElement Body):IRequest<Response<ClaimResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.ClaimModule, SupplyChainCentralAudit.Transition, "Claim");
    // The repository enlists with the id of the aggregate it wrote; the command already names it.
    public Guid? CentralAuditEntityId(object? response) => ClaimId;
}
