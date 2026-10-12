using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.ESignReauth;

/// <summary>MOD-0022 E1 — machine-readable failure codes (ESIGN-BUNDLE §4.27) and audit event names.</summary>
public static class ESignReauthCodes
{
    public const string ReauthFailed = "ESIGN_REAUTH_FAILED";
    public const string AccountLocked = "ESIGN_ACCOUNT_LOCKED";
    public const string AccountDisabled = "ESIGN_ACCOUNT_DISABLED";
    public const string ActorNotTenantUser = "ESIGN_ACTOR_NOT_TENANT_USER";
    public const string WindowExpired = "ESIGN_REAUTH_WINDOW_EXPIRED";
    public const string IntentRequiresPassword = "ESIGN_INTENT_REQUIRES_PASSWORD";
    public const string UserNameMismatch = "ESIGN_USERNAME_MISMATCH";
    public const string TicketInvalid = "ESIGN_TICKET_INVALID";
    public const string ReauthUnavailable = "ESIGN_REAUTH_UNAVAILABLE";

    public const string TenantUserActorType = "tenant_user";

    // authAuditLogs event names (pack §8 E1). Metadata never carries the password or the plain ticket.
    public const string AuditSucceeded = "tenant_esign_reauth_succeeded";
    public const string AuditPasswordFailed = "tenant_esign_reauth_password_failed";
    public const string AuditBlockedByLockout = "tenant_esign_reauth_blocked_by_lockout";
    public const string AuditLockoutStarted = "tenant_esign_reauth_lockout_started";
    public const string AuditDeniedInactive = "tenant_esign_reauth_denied_inactive";
    public const string AuditWindowTicketIssued = "tenant_esign_window_ticket_issued";
    public const string AuditWindowTicketRefused = "tenant_esign_window_ticket_refused";
    public const string AuditTicketConsumed = "tenant_esign_ticket_consumed";
    public const string AuditTicketRejected = "tenant_esign_ticket_rejected";
    public const string AuditWindowsRevoked = "tenant_esign_windows_revoked";
}

/// <summary>Successful re-authentication / window ticket (ESIGN-BUNDLE §2.11). Only place the plain ticket exists.</summary>
public sealed record ESignReauthTicketDto(
    string Ticket,
    DateTime ExpiresAtUtc,
    DateTime PasswordVerifiedAtUtc,
    string Kind,
    DateTime? WindowExpiresAtUtc)
{
    public override string ToString() => $"ESignReauthTicketDto {{ Kind = {Kind}, ExpiresAtUtc = {ExpiresAtUtc:O} }}";
}

/// <summary>Result of the internal consume (ESIGN-BUNDLE §4.3).</summary>
public sealed record ESignTicketConsumedDto(string Kind, string Intent, DateTime PasswordVerifiedAtUtc, DateTime IssuedAtUtc);

/// <summary>
/// POST api/tenant-auth/esign/reauth. The user and tenant come from the validated JWT (sub / tenant_id), never from the
/// body — the body has no user name. <see cref="ToString"/> never prints the password.
/// </summary>
public sealed record ReauthenticateForESignCommand(
    Guid UserId,
    Guid TenantId,
    string? ActorType,
    string Password,
    string Intent,
    int? OpenWindowMinutes,
    string RequestIp,
    string? UserAgent) : IRequest<Response<ESignReauthTicketDto>>
{
    public override string ToString() =>
        $"ReauthenticateForESignCommand {{ UserId = {UserId}, TenantId = {TenantId}, Intent = {Intent}, OpenWindowMinutes = {OpenWindowMinutes} }}";
}

/// <summary>
/// POST api/tenant-auth/esign/ticket — a ticket from an open KS-3 window, without the password. CT-D7: the signer
/// re-types their user name (one signature component, Part 11 §11.200(a)(1)(i)); it must name the session user.
/// </summary>
public sealed record IssueESignWindowTicketCommand(
    Guid UserId,
    Guid TenantId,
    string? ActorType,
    string Intent,
    string UserName,
    string RequestIp,
    string? UserAgent) : IRequest<Response<ESignReauthTicketDto>>;

/// <summary>POST internal/esign-tickets/consume (S2S, internal API key). <see cref="ToString"/> never prints the ticket.</summary>
public sealed record ConsumeESignTicketCommand(
    string Ticket,
    Guid TenantId,
    Guid UserId,
    string Intent,
    string ConsumedBy) : IRequest<Response<ESignTicketConsumedDto>>
{
    public override string ToString() =>
        $"ConsumeESignTicketCommand {{ TenantId = {TenantId}, UserId = {UserId}, Intent = {Intent}, ConsumedBy = {ConsumedBy} }}";
}
