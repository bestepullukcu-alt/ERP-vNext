using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Contracts;

public interface IAdminUserInvitationService
{
    Task<AdminUserInvitationResult> InviteAsync(Tenant tenant, TenantAdminUser adminUser, CancellationToken cancellationToken);
}

public sealed record AdminUserInvitationResult(
    string LoginUrl,
    string TemporaryPassword,
    bool UserProvisioned,
    bool InvitationEmailSent);
