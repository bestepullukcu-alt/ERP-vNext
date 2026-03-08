using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDto>
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

    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        var existing = await _roleRepository.GetByNameAndTenantAsync(request.Name, _tenantContext.TenantId, ct);
        if (existing != null) throw new InvalidOperationException("Rol adı zaten kullanımda.");

        var role = new Role(request.Name, request.DisplayName, request.Description, _tenantContext.TenantId);
        var created = await _roleRepository.CreateAsync(role, ct);

        return new RoleDto(created.Id, created.Name, created.DisplayName, created.Description, created.IsSystem, 0);
    }
}
