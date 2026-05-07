using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Queries;

public sealed record GetAllRolesQuery() : IRequest<Response<List<RoleDto>>>;
