using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Commands;

public sealed record CreatePermissionCommand(
    string Module,
    string Resource,
    string Action,
    string DisplayName,
    string? Description
) : IRequest<Response<PermissionDto>>;
