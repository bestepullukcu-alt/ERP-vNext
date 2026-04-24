using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Queries;

public sealed record GetPermissionsByModuleQuery(string Module) : IRequest<List<PermissionDto>>;
