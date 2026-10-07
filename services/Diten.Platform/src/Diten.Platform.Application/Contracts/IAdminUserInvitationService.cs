using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Contracts;

public interface IAdminUserInvitationService
{
    Task<AdminUserInvitationResult> InviteAsync(Tenant tenant, TenantAdminUser adminUser, CancellationToken cancellationToken);
}

/// <param name="SetPasswordUrl">BL-454 slice 2 stage D — the administrator's one-time set-password link (no password is
/// ever created or sent). Null when no link may be sent from this server (<paramref name="EmailRefusalCode"/>). Surfaced to
/// the operator only in Development when the e-mail did not leave, as the tenant Users screen does.</param>
/// <param name="EmailRefusalCode">Why the invitation e-mail was not sent at all, by name; null when it was queued or the
/// queue itself failed (that is logged with its own reason).</param>
public sealed record AdminUserInvitationResult(
    string LoginUrl,
    string? SetPasswordUrl,
    bool UserProvisioned,
    bool InvitationEmailSent,
    string? EmailRefusalCode = null);
