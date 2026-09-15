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
    private readonly IRbacAuditRecorder _rbacAudit;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogger<DeleteRoleCommandHandler> _logger;

    public DeleteRoleCommandHandler(
        IRoleRepository roleRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IRbacAuditRecorder rbacAudit,
        ICurrentUserAccessor currentUser,
        ILogger<DeleteRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _rbacAudit = rbacAudit;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        // BL-412 F1 — DELETE api/roles/{id} is a person path: the soft delete names who deleted the role (UpdatedBy),
        // the same actor id the role_deleted audit row carries.
        if (_currentUser.UserId is not { } actorId)
            return Response<NoContent>.Fail("An authenticated user is required to delete a role.", 401);

        var role = await _roleRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (role == null) return Response<NoContent>.Fail("Role not found.", 404);

        if (role.IsSystem) return Response<NoContent>.Fail("System roles cannot be deleted.", 403);

        await _roleRepository.DeleteAsync(request.Id, _tenantContext.TenantId, actorId.ToString(), ct);

        // FU13 — deleting a role removes it from every holder; bump to invalidate cached snapshots immediately.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // FEAT-AUDIT-RBAC — a role was deleted.
        await _rbacAudit.RecordAsync("role_deleted", _tenantContext.TenantId,
            new { roleId = request.Id, roleName = role.Name }, ct);

        return Response<NoContent>.Success(204);
    }
}
