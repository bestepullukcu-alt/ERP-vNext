using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed record TransitionProgramLifecycleCommand(Guid Id, ProgramLifecycleState TargetState, int ExpectedVersion) : IRequest<Response<ProgramDto>>;
