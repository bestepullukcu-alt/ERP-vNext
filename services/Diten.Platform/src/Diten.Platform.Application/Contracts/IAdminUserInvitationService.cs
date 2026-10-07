using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Contracts;

public interface IAdminUserInvitationService
{
    /// <param name="trigger">Who is inviting. BL-454 stage D FIX1 — only the operator's explicit "Invite" may reset an account
    /// that already exists; the tenant-created event only creates (an existing account is left exactly as it is).</param>
    Task<AdminUserInvitationResult> InviteAsync(Tenant tenant, TenantAdminUser adminUser, AdminInvitationTrigger trigger, CancellationToken cancellationToken);
}

public enum AdminInvitationTrigger
{
    /// <summary>The tenant screen's "Invite": an explicit administrator action; an existing account is reset (BL-529, audited).</summary>
    Operator,

    /// <summary>The tenant-created event (redelivered, re-published, retried): create only, never touch an existing account.</summary>
    TenantCreatedEvent
}

public static class AdminInvitationRefusals
{
    /// <summary>The event path found an account already there and changed nothing (no link, no e-mail).</summary>
    public const string AccountExists = "ADMIN_ACCOUNT_EXISTS";
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
