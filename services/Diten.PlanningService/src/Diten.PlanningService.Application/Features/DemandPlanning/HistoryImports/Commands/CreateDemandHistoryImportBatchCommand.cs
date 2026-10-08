using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record CreateDemandHistoryImportBatchCommand(
    Guid TenantId, Guid ActorId, Guid SelectedLegalEntityHint,
    string SourceSystem, string SourceFileName,
    DateOnly ScopeFrom, DateOnly ScopeThrough,
    IReadOnlyList<string> WarehouseScope,
    byte[] FileBytes, string IdempotencyKey)
    : IRequest<Response<HistoryImportBatchResult>>;
