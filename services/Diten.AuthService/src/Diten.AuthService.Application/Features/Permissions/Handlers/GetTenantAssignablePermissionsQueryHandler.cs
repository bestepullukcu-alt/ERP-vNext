using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Permissions.Queries;
using Diten.AuthService.Domain.Authorization;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Handlers;

// FEAT-ROLEPERMS-TENANT-SCOPE — returns only the permissions a tenant role may be granted, filtered by the
// SAME DefaultRolePermissionTemplate.IsTenantAssignable boundary the assign guard uses, so the UI catalog and
// the backend guard can never disagree. GetAllAsync already excludes soft-deleted rows.
public sealed class GetTenantAssignablePermissionsQueryHandler
    : IRequestHandler<GetTenantAssignablePermissionsQuery, Response<List<PermissionDto>>>
{
    private readonly IPermissionRepository _permissionRepository;

    public GetTenantAssignablePermissionsQueryHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Response<List<PermissionDto>>> Handle(GetTenantAssignablePermissionsQuery request, CancellationToken ct)
    {
        var permissions = await _permissionRepository.GetAllAsync(ct);
        var assignable = permissions
            .Where(DefaultRolePermissionTemplate.IsTenantAssignable)
            .Select(p => new PermissionDto(p.Id, p.Module, p.Resource, p.Action, p.Key, p.DisplayName, p.Description))
            .ToList();

        return Response<List<PermissionDto>>.Success(assignable);
    }
}
