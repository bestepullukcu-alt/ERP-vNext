using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Permissions.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Handlers;

public sealed class GetPermissionsByModuleQueryHandler : IRequestHandler<GetPermissionsByModuleQuery, Response<List<PermissionDto>>>
{
    private readonly IPermissionRepository _permissionRepository;

    public GetPermissionsByModuleQueryHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Response<List<PermissionDto>>> Handle(GetPermissionsByModuleQuery request, CancellationToken ct)
    {
        var permissions = await _permissionRepository.GetByModuleAsync(request.Module, ct);
        return Response<List<PermissionDto>>.Success(permissions.Select(p => new PermissionDto(p.Id, p.Module, p.Resource, p.Action, p.Key, p.DisplayName, p.Description)).ToList());
    }
}
