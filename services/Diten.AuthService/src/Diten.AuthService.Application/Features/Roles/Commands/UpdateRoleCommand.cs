using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Commands;

public sealed record UpdateRoleCommand(
    Guid Id,
    string DisplayName,
    string? Description
) : IRequest<RoleDto>;
