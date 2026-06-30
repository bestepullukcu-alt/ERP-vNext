using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Response<NoContent>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteRoleCommandHandler> _logger;

    public DeleteRoleCommandHandler(
        IRoleRepository roleRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        ILogger<DeleteRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (role == null) return Response<NoContent>.Fail("Role not found.", 404);

        if (role.IsSystem) return Response<NoContent>.Fail("System roles cannot be deleted.", 403);

        await _roleRepository.DeleteAsync(request.Id, _tenantContext.TenantId, ct);

        // FU13 — deleting a role removes it from every holder; bump to invalidate cached snapshots immediately.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);
        return Response<NoContent>.Success(204);
    }
}
