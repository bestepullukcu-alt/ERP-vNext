using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Queries;

public sealed record GetAllPermissionsQuery() : IRequest<Response<List<PermissionDto>>>;
