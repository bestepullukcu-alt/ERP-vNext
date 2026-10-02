using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Response<UserDto>>
{
    // Invitation set-password links are valid for 7 days.
    private static readonly TimeSpan InvitationTokenLifetime = TimeSpan.FromDays(7);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly ITenantContext _tenantContext;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IHostEnvironment _environment;
    private readonly ITenantUserInvitationEmailService _invitationEmailService;
    private readonly IUserAuditRecorder _audit;
    private readonly IUserQuotaClient _quota;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyService passwordPolicyService,
        ITenantContext tenantContext,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        IHostEnvironment environment,
        ITenantUserInvitationEmailService invitationEmailService,
        IUserAuditRecorder audit,
        IUserQuotaClient quota,
        ILogger<CreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _tenantContext = tenantContext;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _environment = environment;
        _invitationEmailService = invitationEmailService;
        _audit = audit;
        _quota = quota;
        _logger = logger;
    }

    // WP-INFRA-AUTH-ACCOUNT-KIND-01 — the stable code the frontend maps; the message is the English fallback.
    public const string PermissionDeniedCode = UserErrorCodes.AccountKindPermissionDenied;

    public async Task<Response<UserDto>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — classifying on create is a SEPARATE right from creating. A supplied kind
        // from a caller without auth.users.account-kind.manage is refused outright (403 PERM_DENIED) — not silently
        // downgraded to Unknown, which would hide a permission gap behind a success. Checked BEFORE the duplicate
        // e-mail probe so an unauthorized caller learns nothing about existing addresses from this path.
        if (!string.IsNullOrWhiteSpace(request.AccountKind) && !request.CallerCanManageAccountKind)
        {
            return Response<UserDto>.Fail(
                "Setting the account kind requires the auth.users.account-kind.manage permission.",
                [new ResponseError(PermissionDeniedCode)],
                403);
        }

        var existing = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);
        // WP-AUTH-INVITED-LIFECYCLE-01 — a LIVE user only: the probe and the users e-mail unique index both ignore
        // soft-deleted accounts, so a deleted user's address opens a NEW account (owner decision A — the old record
        // and its history stay; its roles belong to the old id and are never carried over). The index is still the
        // last word: a concurrent insert that wins the race surfaces here as 409 too, not as a 500.
        if (existing != null) return UserLifecycle.EmailTakenRefusal<UserDto>();

        // BL-459 — the plan's user limit, BEFORE anything is written: at the limit nothing is created and no invitation
        // leaves. users.max is Platform's live count of this tenant's Active + Invited users (F1): the question reserves
        // nothing, so a create that fails afterwards has nothing to give back. Platform unreachable / usage unknown = the
        // create proceeds (the client logged quota_unavailable) — owner decision.
        var seat = await _quota.TryConsumeUserSeatAsync(_tenantContext.TenantId, $"user-create:{Guid.NewGuid():N}", ct);
        if (seat.Outcome == UserQuotaOutcome.LimitExceeded)
        {
            _logger.LogInformation("User create refused at the plan's user limit. TenantId={TenantId} Max={Max} Current={Current}",
                _tenantContext.TenantId, seat.Limit, seat.Current);
            return UserLifecycle.QuotaExceededRefusal<UserDto>(seat.Limit, seat.Current);
        }

        try
        {
            return string.IsNullOrWhiteSpace(request.Password)
                ? await CreateByInvitationAsync(request, ct)
                : await CreateSelfServiceAsync(request, ct);
        }
        catch (DuplicateUserEmailException)
        {
            return UserLifecycle.EmailTakenRefusal<UserDto>();
        }
    }

    // The kind the new account gets: the caller's explicit, permitted choice, else Unknown. Parsed from the NAME the
    // validator vetted — no Human/Service literal here (AccountKindCreationPathsGuardTests keeps it that way).
    private static AccountKind ResolveInitialKind(CreateUserCommand request)
    {
        if (request.CallerCanManageAccountKind
            && !string.IsNullOrWhiteSpace(request.AccountKind)
            && Enum.TryParse<AccountKind>(request.AccountKind.Trim(), ignoreCase: true, out var kind)
            && Enum.IsDefined(kind))
        {
            return kind;
        }

        return AccountKind.Unknown;
    }

    // ── Self-service: admin supplies the password; user is active immediately (unchanged behavior). ──
    private async Task<Response<UserDto>> CreateSelfServiceAsync(CreateUserCommand request, CancellationToken ct)
    {
        await _passwordPolicyService.ValidateTenantPasswordAsync(_tenantContext.TenantId, null, request.Password!, "create_user", ct);
        var hashedPassword = _passwordHasher.Hash(request.Password!);
        var user = new User(request.Email, hashedPassword, request.FirstName, request.LastName, _tenantContext.TenantId);
        user.SetAccountKind(ResolveInitialKind(request));

        var created = await _userRepository.CreateAsync(user, ct);

        _logger.LogInformation("User created (self-service). Id={Id} AccountKind={AccountKind}", created.Id, created.AccountKind);

        // BL-456 — the account exists: audit it (authAuditLogs + Platform central log). Never fails the create.
        await _audit.RecordAsync(UserAuditEvents.Created, _tenantContext.TenantId, created.Id,
            new Dictionary<string, object?> { ["accountKind"] = created.AccountKind.ToString() }, ct);

        return Response<UserDto>.Success(
            new UserDto(created.Id, created.Email, created.FirstName, created.LastName, created.IsActive, new List<string>(), created.TenantId,
                AccountKind: created.AccountKind.ToString(), Status: UserLifecycle.StatusOf(created)),
            201);
    }

    // ── Invitation: no password. The user sets their own via the emailed set-password link. ──
    private async Task<Response<UserDto>> CreateByInvitationAsync(CreateUserCommand request, CancellationToken ct)
    {
        // Placeholder hash — the column is non-null. It is never usable for login: the account is
        // inactive + must-change-password until the set-password link is redeemed.
        var placeholderHash = _passwordHasher.Hash(_tokenService.GenerateRefreshToken());
        var user = new User(request.Email, placeholderHash, request.FirstName, request.LastName, _tenantContext.TenantId);

        var setupToken = _tokenService.GenerateRefreshToken();
        user.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.Add(InvitationTokenLifetime));
        user.Deactivate();
        user.RequirePasswordChange(null);
        user.SetAccountKind(ResolveInitialKind(request));

        var created = await _userRepository.CreateAsync(user, ct);

        (string? SetupUrl, bool EmailSent) setupDelivery = (null, false);
        try
        {
            setupDelivery = await SendInvitationAsync(created.Email, setupToken, ct);
        }
        finally
        {
            // BL-456 — the user exists from CreateAsync on, so the audit row must exist even when the e-mail throws
            // (production re-throws an SMTP failure). Ids and facts only; the invitee's address stays out (no PII).
            await _audit.RecordAsync(UserAuditEvents.Invited, _tenantContext.TenantId, created.Id,
                new Dictionary<string, object?>
                {
                    ["accountKind"] = created.AccountKind.ToString(),
                    ["emailSent"] = setupDelivery.EmailSent
                }, ct);
        }

        _logger.LogInformation(
            "User invited (set-password). Id={Id} EmailSent={EmailSent}", created.Id, setupDelivery.EmailSent);
        if (setupDelivery.SetupUrl is not null)
        {
            // Development convenience only — prod never logs the link/token.
            _logger.LogInformation("[DEV] Tenant set-password link for {Email}: {SetupUrl}", created.Email, setupDelivery.SetupUrl);
        }

        return Response<UserDto>.Success(
            new UserDto(created.Id, created.Email, created.FirstName, created.LastName, created.IsActive, new List<string>(), created.TenantId,
                created.LastLoginAt, created.FailedLoginAttempts, created.MustChangePassword, "TenantPolicy", setupDelivery.SetupUrl,
                created.AccountKind.ToString(), UserLifecycle.StatusOf(created)),
            201);
    }

    // Mirrors PlatformAuthController.SendSetupLinkAsync: in Development, SMTP failures are swallowed and
    // the link is surfaced/logged; in Production the link is null and a send failure propagates.
    private async Task<(string? SetupUrl, bool EmailSent)> SendInvitationAsync(string email, string setupToken, CancellationToken ct)
    {
        var setupUrl = _invitationEmailService.BuildTenantSetPasswordUrl(email, setupToken);
        try
        {
            await _invitationEmailService.SendTenantUserInvitationAsync(email, setupToken, ct);
            return (_environment.IsDevelopment() ? setupUrl : null, true);
        }
        catch when (_environment.IsDevelopment())
        {
            return (setupUrl, false);
        }
    }
}
