using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Response<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly IRbacAuditRecorder _rbacAudit;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogger<CreateRoleCommandHandler> _logger;

    public CreateRoleCommandHandler(
        IRoleRepository roleRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IRbacAuditRecorder rbacAudit,
        ICurrentUserAccessor currentUser,
        ILogger<CreateRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _rbacAudit = rbacAudit;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<RoleDto>> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        // BL-412 — a role created through POST api/roles records the person who created it (same actor id as the
        // role_created audit row). System roles are created by UpsertSystemRoleAsync/DataSeeder, never here.
        if (_currentUser.UserId is not { } actorId)
            return Response<RoleDto>.Fail("An authenticated user is required to create a role.", 401);

        var existing = await _roleRepository.GetByNameAndTenantAsync(request.Name, _tenantContext.TenantId, ct);
        if (existing != null) return Response<RoleDto>.Fail("Role name is already in use.", 409);

        var role = new Role(request.Name, request.DisplayName, request.Description, _tenantContext.TenantId)
        {
            CreatedBy = actorId.ToString()
        };
        var created = await _roleRepository.CreateAsync(role, ct);

        // FU13 — a role mutation changes the tenant's authorization surface; bump to invalidate cached snapshots.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // FEAT-AUDIT-RBAC — a new role was created.
        await _rbacAudit.RecordAsync("role_created", _tenantContext.TenantId,
            new { roleId = created.Id, roleName = created.Name, displayName = created.DisplayName, description = created.Description }, ct);

        return Response<RoleDto>.Success(new RoleDto(created.Id, created.Name, created.DisplayName, created.Description, created.IsSystem, 0), 201);
    }
}
