using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class InviteTenantAdminUserCommandHandler : IRequestHandler<InviteTenantAdminUserCommand, Response<TenantAdminUserDto>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAdminUserInvitationService _invitationService;
    private readonly IQuotaService _quotaService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<InviteTenantAdminUserCommandHandler> _logger;

    public InviteTenantAdminUserCommandHandler(
        ITenantRegistryRepository repository,
        ICurrentUserContext currentUser,
        IAdminUserInvitationService invitationService,
        IQuotaService quotaService,
        IHostEnvironment environment,
        ILogger<InviteTenantAdminUserCommandHandler> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _invitationService = invitationService;
        _quotaService = quotaService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<Response<TenantAdminUserDto>> Handle(InviteTenantAdminUserCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Response<TenantAdminUserDto>.Fail("Tenant not found.", 404);
        }

        TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        var user = tenant.AdminUsers.FirstOrDefault(item => item.Id == request.AdminUserId);
        if (user == null)
        {
            return Response<TenantAdminUserDto>.Fail("Tenant admin user not found.", 404);
        }

        // BL-454 stage D FIX2 K11 — the link root FIRST: an invitation that may not be sent from this server spends nothing
        // (no users quota, no AuthService call) and the operator is told which rule refused, by name.
        if (_invitationService.LinkRootRefusal() is { } rootRefusal)
        {
            return Response<TenantAdminUserDto>.Fail(RootRefusalMessage, 422, rootRefusal);
        }

        var now = DateTimeOffset.UtcNow;
        var operationId = $"tenant-admin-user:{user.Id}:invite";
        var sourceReference = user.Id.ToString();
        var correlationId = Guid.NewGuid().ToString();
        // BL-459 F1 — users.max is a COUNT of AuthService's Active + Invited users: the consume below only asks whether
        // one more fits, it reserves nothing, so a failed invitation has nothing to give back.

        if (!TenantAdminUserSupport.CountsTowardsUsersQuota(user))
        {
            try
            {
                var syncResponse = await SyncUsersQuotaStateAsync(
                    tenant.Id,
                    correlationId,
                    cancellationToken);

                if (!syncResponse.IsSuccessful)
                {
                    return Response<TenantAdminUserDto>.Fail(syncResponse.Errors, syncResponse.StatusCode);
                }

                var consumeResponse = await _quotaService.TryConsumeAsync(new TryConsumeQuotaRequest(
                    tenant.Id,
                    QuotaKeys.UsersMax,
                    1,
                    "TenantAdminUserInvite",
                    operationId,
                    sourceReference,
                    "Tenant admin user invited.",
                    _currentUser.ActorName,
                    correlationId), cancellationToken);

                if (!consumeResponse.IsSuccessful)
                {
                    var isDuplicate = consumeResponse.Errors.Any(error => string.Equals(error, QuotaErrorCodes.DuplicateOperation, StringComparison.OrdinalIgnoreCase));
                    if (!isDuplicate)
                    {
                        return Response<TenantAdminUserDto>.Fail(consumeResponse.Errors, consumeResponse.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Tenant admin users quota reservation failed. TenantId={TenantId} AdminUserId={AdminUserId}",
                    tenant.Id,
                    user.Id);
                return Response<TenantAdminUserDto>.Fail("Tenant admin users quota could not be verified.", 503);
            }
        }

        AdminUserInvitationResult invitation;
        try
        {
            invitation = await _invitationService.InviteAsync(tenant, user, AdminInvitationTrigger.Operator, cancellationToken);
        }
        catch (Exception ex)
        {
            // Previously swallowed silently — left us blind to the real provisioning failure. Log it now.
            _logger.LogError(
                ex,
                "Tenant admin invitation failed. TenantId={TenantId} AdminUserId={AdminUserId}",
                tenant.Id,
                user.Id);
            return Response<TenantAdminUserDto>.Fail("Tenant admin invitation could not be completed.", 502);
        }

        // BL-454 stage D FIX1/FIX2 — refusals that changed nothing for the account, told to the operator by name: the link
        // root (also checked above; the service checks it again), AuthService refusing the platform's own tenant (K11), and
        // an AuthService answer that cannot be trusted (E5). FIX3 — "the account exists" too: the operator's door resets an
        // existing account and never answers it, so it can only come from a wrong AuthService (defence).
        if (invitation.EmailRefusalCode is { } refusal)
        {
            await RecordStepAsync(tenant.Id, user.Id, "Failed", $"Invitation not sent ({refusal}).", stampInvitedAt: false, null, cancellationToken);
            return refusal switch
            {
                AdminInvitationRefusals.PlatformTenantRefused =>
                    Response<TenantAdminUserDto>.Fail("The platform tenant has no tenant administrators.", 422, refusal),
                AdminInvitationRefusals.AuthAnswerInvalid =>
                    Response<TenantAdminUserDto>.Fail("AuthService gave an answer that cannot be used; nothing was sent.", 502, refusal),
                AdminInvitationRefusals.AccountExists =>
                    Response<TenantAdminUserDto>.Fail("AuthService did not re-invite the existing account; nothing was sent.", 502, refusal),
                _ => Response<TenantAdminUserDto>.Fail(RootRefusalMessage, 422, refusal)
            };
        }

        user.Status = TenantAdminUserStatus.Invited;
        user.InvitedAt = now;
        user.LastInvitationDispatchId = invitation.InvitationDispatchId;
        user.UpdatedAt = now;
        tenant.ActiveUserCount = TenantAdminUserSupport.CountUsersQuotaUsage(tenant);
        TenantAdminUserSupport.AddActivity(
            tenant,
            "tenant.admin_user.invited",
            $"Admin invitation sent for '{user.Email}'. Login: {invitation.LoginUrl}",
            _currentUser.ActorName,
            now);
        try
        {
            await _repository.UpdateAsync(tenant, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tenant admin invitation state update failed. TenantId={TenantId} AdminUserId={AdminUserId}",
                tenant.Id,
                user.Id);
            return Response<TenantAdminUserDto>.Fail("Tenant admin invitation state could not be saved.", 502);
        }

        // BL-454 stage D FIX2 K6 — the operator's "Invite" writes the invitation step too (targeted, K7), with the dispatch that
        // now carries the current invitation (FIX3 (1)).
        var stepRecorded = await RecordStepAsync(
            tenant.Id, user.Id,
            invitation.InvitationEmailSent ? "Completed" : "Failed",
            invitation.InvitationEmailSent
                ? "Invitation sent by an operator: a one-time set-password link."
                : "The account was reset by an operator but the invitation e-mail did not leave. Use \"Invite\" again.",
            stampInvitedAt: invitation.InvitationEmailSent, invitation.InvitationDispatchId, cancellationToken);

        var dto = TenantAdminUserSupport.ToDto(user);

        // BL-454 stage D FIX2 K14 — whether the e-mail left is told to the operator in EVERY environment: after a reset the
        // administrator's old password no longer works, and an operator who believes a mail went out waits for nothing. The
        // link itself is surfaced only in Development (the tenant-side Users invite dev-fallback). No temporary password.
        dto = dto with
        {
            EmailSent = invitation.InvitationEmailSent,
            TemporaryPassword = null,
            InvitationStepNotRecorded = stepRecorded ? null : true
        };
        if (!invitation.InvitationEmailSent && _environment.IsDevelopment())
        {
            dto = dto with { LoginUrl = invitation.SetPasswordUrl ?? invitation.LoginUrl };
        }

        return Response<TenantAdminUserDto>.Success(dto);
    }

    /// <summary>The 422 text for a refused link root — the rule, not more: outside Development the address must be https
    /// and must not be this machine (localhost, a loopback or an unspecified address).</summary>
    internal const string RootRefusalMessage =
        "The invitation link cannot be sent from this server: AuthService:FrontendBaseUrl must be an https address that is not this machine (localhost, 127.0.0.1, 0.0.0.0, ::).";

    /// <returns>Whether the step was written. FIX3 — a failed write is not swallowed: an error in the log, and the operator
    /// is told (<see cref="TenantAdminUserDto.InvitationStepNotRecorded"/>).</returns>
    private async Task<bool> RecordStepAsync(
        Guid tenantId, Guid adminUserId, string status, string detail, bool stampInvitedAt, Guid? invitationDispatchId, CancellationToken ct)
    {
        var at = DateTimeOffset.UtcNow;
        try
        {
            await _repository.RecordAdminInvitationAsync(
                tenantId, adminUserId, TenantProvisioningStep.AdminInvitationKey, status, detail, at, stampInvitedAt, invitationDispatchId,
                new TenantActivityEvent { EventType = "tenant.admin_user.invitation_step", Message = detail, At = at, Actor = _currentUser.ActorName }, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "tenant.admin_invitation.step_write_failed TenantId={TenantId} AdminUserId={AdminUserId}", tenantId, adminUserId);
            return false;
        }
    }

    private async Task<Response<IReadOnlyList<QuotaStatusDto>>> SyncUsersQuotaStateAsync(
        Guid tenantId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _quotaService.SyncTenantQuotaLimitsAsync(
                tenantId,
                "TenantAdminUserInvite",
                "Sync users quota before tenant admin invitation.",
                _currentUser.ActorName,
                correlationId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Tenant admin users quota limit sync failed; trying quota initialization fallback. TenantId={TenantId}",
                tenantId);

            return await _quotaService.InitializeTenantQuotasAsync(
                tenantId,
                "TenantAdminUserInvite",
                "Initialize users quota before tenant admin invitation.",
                _currentUser.ActorName,
                correlationId,
                cancellationToken);
        }
    }
}
