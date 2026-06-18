namespace Diten.AuthService.Application.Common.Interfaces;

// Tenant-user invitation email (mirrors IPlatformAuthEmailService). Sends a "set your password"
// link — NOT a temporary password — pointing at the tenant set-password page.
public interface ITenantUserInvitationEmailService
{
    string BuildTenantSetPasswordUrl(string email, string setupToken);
    Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct);
}
