using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed record TransitionProjectLifecycleCommand(Guid Id, ProjectLifecycleState TargetState, int ExpectedVersion) : IRequest<Response<ProjectDto>>;
