using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Permissions.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Handlers;

public sealed class CreatePermissionCommandHandler : IRequestHandler<CreatePermissionCommand, PermissionDto>
{
    private readonly IPermissionRepository _permissionRepository;

    public CreatePermissionCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<PermissionDto> Handle(CreatePermissionCommand request, CancellationToken ct)
    {
        var key = $"{request.Module}.{request.Resource}.{request.Action}".ToLower();
        var existing = await _permissionRepository.GetByKeyAsync(key, ct);
        if (existing != null) throw new InvalidOperationException("Yetki anahtarı zaten tanımlı.");

        var permission = new Permission(request.Module, request.Resource, request.Action, request.DisplayName, request.Description);
        var created = await _permissionRepository.CreateAsync(permission, ct);

        return new PermissionDto(created.Id, created.Module, created.Resource, created.Action, created.Key, created.DisplayName, created.Description);
    }
}
