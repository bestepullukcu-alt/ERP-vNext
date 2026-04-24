using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Queries;

public sealed record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto>;
