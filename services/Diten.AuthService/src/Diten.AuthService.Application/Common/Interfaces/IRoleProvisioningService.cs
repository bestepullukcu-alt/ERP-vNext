namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRoleProvisioningService
{
    Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct = default);
}
