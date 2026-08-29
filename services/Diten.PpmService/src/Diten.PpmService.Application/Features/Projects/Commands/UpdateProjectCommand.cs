using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed record UpdateProjectCommand(Guid Id, string Code, string Name, string? Description, ProjectParentType ParentType, Guid ParentId, string? VisibilityPolicyKey, int ExpectedVersion) : IRequest<Response<ProjectDto>>;
