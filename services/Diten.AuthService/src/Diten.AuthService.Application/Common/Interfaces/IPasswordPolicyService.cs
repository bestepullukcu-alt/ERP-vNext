using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPasswordPolicyService
{
    Task ValidateTenantPasswordAsync(Guid tenantId, Guid? userId, string password, string context, CancellationToken ct);
    string GenerateTemporaryPassword(TenantLoginSettingsSnapshot settings);
}
