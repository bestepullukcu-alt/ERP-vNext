using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Permissions.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Handlers;

public sealed class DeletePermissionCommandHandler : IRequestHandler<DeletePermissionCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public DeletePermissionCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(DeletePermissionCommand request, CancellationToken ct)
    {
        var permission = await _permissionRepository.GetByIdAsync(request.Id, ct);
        if (permission == null) throw new KeyNotFoundException("Yetki bulunamadı.");

        if (permission.IsSystem) throw new InvalidOperationException("Sistem yetkileri silinemez.");

        await _permissionRepository.DeleteAsync(request.Id, ct);
        return Unit.Value;
    }
}
