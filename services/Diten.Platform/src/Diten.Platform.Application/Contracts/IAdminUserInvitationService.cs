using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Contracts;

public interface IAdminUserInvitationService
{
    /// <param name="trigger">Who is inviting. BL-454 stage D FIX1 — only the operator's explicit "Invite" may reset an account
    /// that already exists; the tenant-created event only creates (an existing account is left exactly as it is).</param>
    Task<AdminUserInvitationResult> InviteAsync(Tenant tenant, TenantAdminUser adminUser, AdminInvitationTrigger trigger, CancellationToken cancellationToken);

    /// <summary>BL-454 stage D FIX2 K11 — whether a set-password link may be sent from this server at all (its configured
    /// root), asked BEFORE anything else is spent on an invitation (the users quota, AuthService). Null: it may.</summary>
    string? LinkRootRefusal();
}

public enum AdminInvitationTrigger
{
    /// <summary>BL-454 stage D FIX2 K8 — the zero value is the SAFE one: an unset trigger is create only, never a reset.</summary>
    Unspecified = 0,

    /// <summary>The tenant screen's "Invite": an explicit administrator action; an existing account is reset (BL-529, audited).
    /// Always given explicitly.</summary>
    Operator,

    /// <summary>The tenant-created event (redelivered, re-published, retried): create only, never touch an existing account.</summary>
    TenantCreatedEvent
}

public static class AdminInvitationRefusals
{
    /// <summary>The event path found an account already there and changed nothing (no link, no e-mail).</summary>
    public const string AccountExists = "ADMIN_ACCOUNT_EXISTS";

    /// <summary>E5 — AuthService answered 200 without a token and without saying "the account exists": not trusted.</summary>
    public const string AuthAnswerInvalid = "AUTH_ANSWER_INVALID";

    /// <summary>K11 — AuthService refused the platform's own tenant (it has no tenant administrators).</summary>
    public const string PlatformTenantRefused = "PLATFORM_TENANT_REFUSED";
}

/// <param name="SetPasswordUrl">BL-454 slice 2 stage D — the administrator's one-time set-password link (no password is
/// ever created or sent). Null when no link may be sent from this server (<paramref name="EmailRefusalCode"/>). Surfaced to
/// the operator only in Development when the e-mail did not leave, as the tenant Users screen does.</param>
/// <param name="EmailRefusalCode">Why the invitation e-mail was not sent at all, by name; null when it was queued or the
/// queue itself failed (that is logged with its own reason).</param>
/// <param name="InvitationDispatchId">BL-454 stage D FIX3 (1) — the notification dispatch that carries this invitation's
/// e-mail, when it was queued; written on the administrator (<see cref="TenantAdminUser.LastInvitationDispatchId"/>).</param>
public sealed record AdminUserInvitationResult(
    string LoginUrl,
    string? SetPasswordUrl,
    bool UserProvisioned,
    bool InvitationEmailSent,
    string? EmailRefusalCode = null,
    Guid? InvitationDispatchId = null);
