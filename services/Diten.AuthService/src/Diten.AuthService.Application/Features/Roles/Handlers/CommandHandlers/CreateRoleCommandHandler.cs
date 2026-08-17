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
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateRoleCommandHandler> _logger;

    public CreateRoleCommandHandler(
        IRoleRepository roleRepository,
        ITenantContext tenantContext,
        ILogger<CreateRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<RoleDto>> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        var existing = await _roleRepository.GetByNameAndTenantAsync(request.Name, _tenantContext.TenantId, ct);
        if (existing != null) return Response<RoleDto>.Fail("Role name is already in use.", 409);

        var role = new Role(request.Name, request.DisplayName, request.Description, _tenantContext.TenantId);
        var created = await _roleRepository.CreateAsync(role, ct);

        return Response<RoleDto>.Success(new RoleDto(created.Id, created.Name, created.DisplayName, created.Description, created.IsSystem, 0), 201);
    }
}
