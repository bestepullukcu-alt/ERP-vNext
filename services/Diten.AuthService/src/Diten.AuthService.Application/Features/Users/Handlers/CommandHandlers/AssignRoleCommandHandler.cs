using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Response<NoContent>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly IRbacAuditRecorder _rbacAudit;
    private readonly ILogger<AssignRoleCommandHandler> _logger;

    public AssignRoleCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IRbacAuditRecorder rbacAudit,
        ILogger<AssignRoleCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _rbacAudit = rbacAudit;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(AssignRoleCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user == null) return Response<NoContent>.Fail("User not found.", 404);

        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role == null) return Response<NoContent>.Fail("Role not found.", 404);

        if (await _userRoleRepository.ExistsAsync(request.UserId, request.RoleId, _tenantContext.TenantId, ct))
            return Response<NoContent>.Success(204);

        await _userRoleRepository.AssignAsync(new UserRole(request.UserId, request.RoleId, _tenantContext.TenantId, "System"), ct);

        // FU13 — invalidate the tenant's cached authorization snapshots by bumping the role-assignment version.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // FEAT-AUDIT-RBAC — a role was newly assigned to a user (idempotent no-op above is not audited).
        await _rbacAudit.RecordAsync("user_role_assigned", _tenantContext.TenantId,
            new { targetUserId = request.UserId, roleId = request.RoleId, roleName = role.Name }, ct);

        return Response<NoContent>.Success(204);
    }
}
