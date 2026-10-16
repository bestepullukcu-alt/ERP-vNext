using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed record PublishDemandRevisionCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, Guid RevisionId, string IdempotencyKey,
    int ExpectedContentVersion, int ExpectedStateVersion)
    : IRequest<Response<RevisionCommandReceipt>>;

public sealed record RevisionCommandReceipt(Guid RevisionId, string Outcome,
    int? StateVersion, string? Checksum);
