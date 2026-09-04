using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record TransitionInitiativeLifecycleCommand(Guid Id, InitiativeLifecycleState TargetState,
    int ExpectedVersion, string? CancellationReasonCode = null, string? HoldReasonCode = null,
    InitiativeClosureRequest? Closure = null) : IRequest<Response<InitiativeLifecycleResult>>;
