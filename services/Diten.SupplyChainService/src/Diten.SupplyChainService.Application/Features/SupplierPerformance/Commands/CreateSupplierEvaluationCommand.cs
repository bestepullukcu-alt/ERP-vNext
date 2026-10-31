using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
public sealed record CreateSupplierEvaluationCommand(JsonElement Body) : IRequest<Response<EvaluationResponse>>, ICentralAuditDeclaration
{
    // MOD-0147 §21: supplier-evaluation.created, path b (supplychain-merkezi-iletim).
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.SupplierPerformanceModule, SupplyChainCentralAudit.Create, "SupplierEvaluation");
    // The repository enlists with the id of the evaluation it wrote; a replay enlists nothing and files nothing.
    public Guid? CentralAuditEntityId(object? response) => null;
}
