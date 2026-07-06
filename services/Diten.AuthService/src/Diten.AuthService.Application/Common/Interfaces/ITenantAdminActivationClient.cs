namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// FIX-TENANT-ADMIN-INVITE-ACTIVATION (Part B) — S2S callback that tells Platform an invited tenant admin has
/// completed its forced first-login password change, so Platform can flip the matching TenantAdminUser from
/// Invited → Active. Best-effort by contract: an unreachable Platform must NEVER fail the password change.
/// </summary>
public interface ITenantAdminActivationClient
{
    Task NotifyActivatedAsync(string email, Guid tenantId, CancellationToken ct);
}
