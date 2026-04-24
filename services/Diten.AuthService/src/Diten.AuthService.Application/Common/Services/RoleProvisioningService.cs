using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Common.Services;

public sealed class RoleProvisioningService : IRoleProvisioningService
{
    private static readonly (string Name, string DisplayName, string Description)[] DefaultRoles =
    [
        ("Admin", "Yönetici", "Tenant yönetimi için varsayılan yönetici rolü"),
        ("Viewer", "İzleyici", "Tenant için minimum okuma rolü")
    ];

    private readonly IRoleRepository _roleRepository;

    public RoleProvisioningService(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct = default)
    {
        foreach (var template in DefaultRoles)
        {
            await _roleRepository.UpsertSystemRoleAsync(
                template.Name,
                template.DisplayName,
                template.Description,
                tenantId,
                ct);
        }
    }
}
