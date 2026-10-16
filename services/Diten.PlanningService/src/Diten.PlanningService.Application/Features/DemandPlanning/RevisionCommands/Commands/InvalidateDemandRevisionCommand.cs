using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed record InvalidateDemandRevisionCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, Guid RevisionId, string IdempotencyKey,
    int ExpectedContentVersion, int ExpectedStateVersion, string ImpactCode,
    string Reason, string EvidenceReference)
    : IRequest<Response<RevisionCommandReceipt>>;
