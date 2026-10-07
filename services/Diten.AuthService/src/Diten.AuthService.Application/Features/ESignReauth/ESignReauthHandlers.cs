using System.Globalization;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.ESignReauth;

/// <summary>
/// MOD-0022 E1 — password re-authentication for an e-signature (pack §8 E1, ESIGN-BUNDLE §4.1).
///
/// <para>The same lockout as login, on the same counter (KS-13): the lockout is checked FIRST (as
/// <c>LoginCommandHandler</c> does), a wrong password calls <see cref="User.RecordLoginFailure(int, int)"/> with the
/// tenant's <c>MaxFailedLoginAttempts</c> / <c>LockoutDurationMinutes</c>, so N failures here or at login (in any mix)
/// lock both. Success resets the counter (<see cref="User.RecordLoginSuccess"/>), exactly as a login does.</para>
///
/// <para>It opens no session: no access token, no refresh token, no cookie; <c>refreshTokens</c> is not touched.
/// KS-11 — a second factor is an extension point only: a future policy would require it here, before the ticket is
/// issued; nothing in v1 asks for one.</para>
/// </summary>
public sealed class ReauthenticateForESignCommandHandler
    : IRequestHandler<ReauthenticateForESignCommand, Response<ESignReauthTicketDto>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthAuditService _audit;
    private readonly ITenantLoginSettingsClient _loginSettings;
    private readonly IESignReauthTicketService _tickets;
    private readonly ILogger<ReauthenticateForESignCommandHandler> _logger;

    public ReauthenticateForESignCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IAuthAuditService audit,
        ITenantLoginSettingsClient loginSettings,
        IESignReauthTicketService tickets,
        ILogger<ReauthenticateForESignCommandHandler> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _loginSettings = loginSettings;
        _tickets = tickets;
        _logger = logger;
    }

    public async Task<Response<ESignReauthTicketDto>> Handle(ReauthenticateForESignCommand request, CancellationToken ct)
    {
        if (!ESignReauthResponses.IsTenantUser(request.ActorType))
        {
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("Only a tenant user can re-authenticate for an e-signature.",
                ESignReauthCodes.ActorNotTenantUser, 403);
        }

        if (!_tickets.IsConfigured)
        {
            _logger.LogError("E-signature re-authentication is not configured (no ticket hashing key).");
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("E-signature re-authentication is temporarily unavailable.",
                ESignReauthCodes.ReauthUnavailable, 503);
        }

        TenantLoginSettingsSnapshot settings;
        try
        {
            settings = await _loginSettings.GetAsync(request.TenantId, ct);
        }
        catch
        {
            await _audit.WriteAsync("tenant_esign_reauth_settings_read_failed", request.UserId, request.TenantId, "{}", ct);
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("E-signature re-authentication is temporarily unavailable.",
                ESignReauthCodes.ReauthUnavailable, 503);
        }

        var user = await _users.GetByIdAndTenantAsync(request.UserId, request.TenantId, ct);
        if (user is null)
        {
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("Re-authentication failed.", ESignReauthCodes.ReauthFailed, 401);
        }

        // 1) Lockout first — a locked account fails even with the correct password (LoginCommandHandler order).
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await _audit.WriteAsync(ESignReauthCodes.AuditBlockedByLockout, user.Id, request.TenantId,
                ESignReauthResponses.Metadata(new { intent = request.Intent }), ct);
            return ESignReauthResponses.Locked<ESignReauthTicketDto>(user.LockoutEnd.Value, remainingAttempts: 0);
        }

        // 2) The password — the login counter on failure.
        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RecordLoginFailure(settings.MaxFailedLoginAttempts, settings.LockoutDurationMinutes);
            await _users.UpdateAsync(user, ct);
            var remaining = Math.Max(0, settings.MaxFailedLoginAttempts - user.FailedLoginAttempts);
            await _audit.WriteAsync(ESignReauthCodes.AuditPasswordFailed, user.Id, request.TenantId,
                ESignReauthResponses.Metadata(new { intent = request.Intent, remainingAttempts = remaining }), ct);

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            {
                await _audit.WriteAsync(ESignReauthCodes.AuditLockoutStarted, user.Id, request.TenantId,
                    ESignReauthResponses.Metadata(new { intent = request.Intent }), ct);
                return ESignReauthResponses.Locked<ESignReauthTicketDto>(user.LockoutEnd.Value, remainingAttempts: 0);
            }

            return Response<ESignReauthTicketDto>.Fail("Re-authentication failed.",
                [new ResponseError(ESignReauthCodes.ReauthFailed, new Dictionary<string, string>
                {
                    ["remainingAttempts"] = remaining.ToString(CultureInfo.InvariantCulture)
                })], 401);
        }

        // 3) A disabled account signs nothing (after the password, as at login).
        if (!user.IsActive)
        {
            await _audit.WriteAsync(ESignReauthCodes.AuditDeniedInactive, user.Id, request.TenantId, "{}", ct);
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("Account is disabled.", ESignReauthCodes.AccountDisabled, 403);
        }

        user.RecordLoginSuccess();
        await _users.UpdateAsync(user, ct);

        var verifiedAt = DateTime.UtcNow;
        var issued = await _tickets.IssueAsync(request.TenantId, user.Id, request.Intent, ESignReauthKind.Password, verifiedAt,
            request.RequestIp, request.UserAgent, ct);

        DateTime? windowExpiresAt = null;
        if (request.OpenWindowMinutes is { } minutes)
        {
            windowExpiresAt = await _tickets.OpenWindowAsync(request.TenantId, user.Id, verifiedAt, minutes, request.UserAgent, ct);
        }

        await _audit.WriteAsync(ESignReauthCodes.AuditSucceeded, user.Id, request.TenantId,
            ESignReauthResponses.Metadata(new { intent = request.Intent, kind = nameof(ESignReauthKind.Password), windowMinutes = request.OpenWindowMinutes }), ct);

        return Response<ESignReauthTicketDto>.Success(new ESignReauthTicketDto(
            issued.Ticket, issued.ExpiresAtUtc, verifiedAt, nameof(ESignReauthKind.Password), windowExpiresAt));
    }
}

/// <summary>
/// MOD-0022 E1 — a ticket from an open KS-3 window (ESIGN-BUNDLE §4.2). No password; the signer re-types the user name
/// (CT-D7). Sample and invalidate intents always need the password. The window must belong to this user AND this browser.
/// </summary>
public sealed class IssueESignWindowTicketCommandHandler
    : IRequestHandler<IssueESignWindowTicketCommand, Response<ESignReauthTicketDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthAuditService _audit;
    private readonly IESignReauthTicketService _tickets;

    public IssueESignWindowTicketCommandHandler(IUserRepository users, IAuthAuditService audit, IESignReauthTicketService tickets)
    {
        _users = users;
        _audit = audit;
        _tickets = tickets;
    }

    public async Task<Response<ESignReauthTicketDto>> Handle(IssueESignWindowTicketCommand request, CancellationToken ct)
    {
        if (!ESignReauthResponses.IsTenantUser(request.ActorType))
        {
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("Only a tenant user can request an e-signature ticket.",
                ESignReauthCodes.ActorNotTenantUser, 403);
        }

        if (!_tickets.IsConfigured)
        {
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("E-signature re-authentication is temporarily unavailable.",
                ESignReauthCodes.ReauthUnavailable, 503);
        }

        if (ESignReauthIntents.RequiresPassword(request.Intent))
        {
            await Refused(request, "intent_requires_password", ct);
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("This e-signature step always needs the password.",
                ESignReauthCodes.IntentRequiresPassword, 400);
        }

        var user = await _users.GetByIdAndTenantAsync(request.UserId, request.TenantId, ct);
        if (user is null)
        {
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("Re-authentication failed.", ESignReauthCodes.ReauthFailed, 401);
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await Refused(request, "locked", ct);
            return ESignReauthResponses.Locked<ESignReauthTicketDto>(user.LockoutEnd.Value, remainingAttempts: 0);
        }

        if (!user.IsActive)
        {
            await Refused(request, "inactive", ct);
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("Account is disabled.", ESignReauthCodes.AccountDisabled, 403);
        }

        // CT-D7 — the re-typed user name must name the session user (user name or e-mail, case-insensitive).
        var typed = request.UserName.Trim();
        var namesSessionUser = string.Equals(typed, user.UserName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(typed, user.Email, StringComparison.OrdinalIgnoreCase);
        if (!namesSessionUser)
        {
            await Refused(request, "username_mismatch", ct);
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("The user name does not match the signed-in user.",
                ESignReauthCodes.UserNameMismatch, 400);
        }

        var window = await _tickets.GetOpenWindowAsync(request.TenantId, user.Id, request.UserAgent, ct);
        if (window is null)
        {
            await Refused(request, "window_expired", ct);
            return ESignReauthResponses.Fail<ESignReauthTicketDto>("The password window is closed; enter the password.",
                ESignReauthCodes.WindowExpired, 409);
        }

        var issued = await _tickets.IssueAsync(request.TenantId, user.Id, request.Intent, ESignReauthKind.Window,
            window.PasswordVerifiedAtUtc, request.RequestIp, request.UserAgent, ct);
        await _audit.WriteAsync(ESignReauthCodes.AuditWindowTicketIssued, user.Id, request.TenantId,
            ESignReauthResponses.Metadata(new { intent = request.Intent, kind = nameof(ESignReauthKind.Window) }), ct);

        return Response<ESignReauthTicketDto>.Success(new ESignReauthTicketDto(
            issued.Ticket, issued.ExpiresAtUtc, window.PasswordVerifiedAtUtc, nameof(ESignReauthKind.Window), window.ExpiresAtUtc));
    }

    private Task Refused(IssueESignWindowTicketCommand request, string reason, CancellationToken ct) =>
        _audit.WriteAsync(ESignReauthCodes.AuditWindowTicketRefused, request.UserId, request.TenantId,
            ESignReauthResponses.Metadata(new { intent = request.Intent, reason }), ct);
}

/// <summary>
/// MOD-0022 E1 — internal single-use consume (ESIGN-BUNDLE §4.3). One code for every miss (unknown, expired, consumed,
/// another user / tenant / intent) so the caller learns nothing about which part failed.
/// </summary>
public sealed class ConsumeESignTicketCommandHandler
    : IRequestHandler<ConsumeESignTicketCommand, Response<ESignTicketConsumedDto>>
{
    private readonly IESignReauthTicketService _tickets;
    private readonly IAuthAuditService _audit;

    public ConsumeESignTicketCommandHandler(IESignReauthTicketService tickets, IAuthAuditService audit)
    {
        _tickets = tickets;
        _audit = audit;
    }

    public async Task<Response<ESignTicketConsumedDto>> Handle(ConsumeESignTicketCommand request, CancellationToken ct)
    {
        if (!_tickets.IsConfigured)
        {
            return ESignReauthResponses.Fail<ESignTicketConsumedDto>("E-signature re-authentication is temporarily unavailable.",
                ESignReauthCodes.ReauthUnavailable, 503);
        }

        var ticket = await _tickets.ConsumeAsync(request.Ticket, request.TenantId, request.UserId, request.Intent, request.ConsumedBy, ct);
        if (ticket is null)
        {
            await _audit.WriteAsync(ESignReauthCodes.AuditTicketRejected, request.UserId, request.TenantId,
                ESignReauthResponses.Metadata(new { intent = request.Intent, consumedBy = request.ConsumedBy }), ct);
            return ESignReauthResponses.Fail<ESignTicketConsumedDto>("The e-signature ticket is not valid.", ESignReauthCodes.TicketInvalid, 409);
        }

        await _audit.WriteAsync(ESignReauthCodes.AuditTicketConsumed, ticket.UserId, ticket.TenantId,
            ESignReauthResponses.Metadata(new { intent = ticket.Intent, kind = ticket.Kind.ToString(), consumedBy = request.ConsumedBy }), ct);

        return Response<ESignTicketConsumedDto>.Success(new ESignTicketConsumedDto(
            ticket.Kind.ToString(), ticket.Intent, ticket.PasswordVerifiedAtUtc, ticket.CreatedAtUtc));
    }
}

internal static class ESignReauthResponses
{
    public static bool IsTenantUser(string? actorType) =>
        string.Equals(actorType, ESignReauthCodes.TenantUserActorType, StringComparison.Ordinal);

    public static Response<T> Fail<T>(string message, string code, int status) =>
        Response<T>.Fail(message, [new ResponseError(code)], status);

    public static Response<T> Locked<T>(DateTime lockoutEndUtc, int remainingAttempts) =>
        Response<T>.Fail("The account is locked.", [new ResponseError(ESignReauthCodes.AccountLocked, new Dictionary<string, string>
        {
            ["lockoutEndUtc"] = DateTime.SpecifyKind(lockoutEndUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            ["remainingAttempts"] = remainingAttempts.ToString(CultureInfo.InvariantCulture)
        })], 423);

    public static string Metadata(object value) => JsonSerializer.Serialize(value);
}
