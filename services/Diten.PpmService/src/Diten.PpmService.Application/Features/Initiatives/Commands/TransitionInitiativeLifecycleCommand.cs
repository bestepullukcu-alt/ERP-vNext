using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record TransitionInitiativeLifecycleCommand(Guid Id, InitiativeLifecycleState TargetState, int ExpectedVersion) : IRequest<Response<InitiativeDto>>;
