using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Queries;

// FEAT-ROLEPERMS-TENANT-SCOPE — the subset of the catalog a tenant role may hold (mirror of the
// AssignPermissionCommandHandler guard), so the Role Permissions screen only offers assignable keys.
public sealed record GetTenantAssignablePermissionsQuery() : IRequest<Response<List<PermissionDto>>>;
