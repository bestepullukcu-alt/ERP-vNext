using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
public sealed record SubmitSupplierEvaluationCommand(Guid EvaluationId, JsonElement Body) : IRequest<Response<EvaluationSubmissionResult>>, ICentralAuditDeclaration
{
    // MOD-0147 §21: supplier-evaluation.published, path b (supplychain-merkezi-iletim).
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.SupplierPerformanceModule, SupplyChainCentralAudit.Transition, "SupplierEvaluation");
    // The repository enlists with the evaluation id; a replay enlists nothing and files nothing.
    public Guid? CentralAuditEntityId(object? response) => null;
}
