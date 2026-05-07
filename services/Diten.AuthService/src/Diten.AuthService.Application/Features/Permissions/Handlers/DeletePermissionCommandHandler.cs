using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Permissions.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Handlers;

public sealed class DeletePermissionCommandHandler : IRequestHandler<DeletePermissionCommand, Response<NoContent>>
{
    private readonly IPermissionRepository _permissionRepository;

    public DeletePermissionCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Response<NoContent>> Handle(DeletePermissionCommand request, CancellationToken ct)
    {
        var permission = await _permissionRepository.GetByIdAsync(request.Id, ct);
        if (permission == null) return Response<NoContent>.Fail("Permission not found.", 404);

        if (permission.IsSystem) return Response<NoContent>.Fail("System permissions cannot be deleted.", 403);

        await _permissionRepository.DeleteAsync(request.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
