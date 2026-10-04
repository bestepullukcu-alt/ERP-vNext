namespace Diten.AuthService.Application.Common.Interfaces;

// Tenant-user invitation email (mirrors IPlatformAuthEmailService). Sends a "set your password"
// link — NOT a temporary password — pointing at the tenant set-password page.
public interface ITenantUserInvitationEmailService
{
    string BuildTenantSetPasswordUrl(string email, string setupToken);
    Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct);

    /// <summary>
    /// BL-454 — the same set-password link, sent as an administrator's PASSWORD RESET rather than an invitation. The
    /// caller states which it is; nothing is inferred from the user record. No default body: an implementation that
    /// silently sent the invitation here would put the invitation's words in a reset mail.
    /// </summary>
    Task SendTenantUserPasswordResetAsync(string email, string setupToken, CancellationToken ct);
}
