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

        // BL-454 stage D FIX1 (2) — the link may not be sent from this server (its root): AuthService was NOT called, nothing
        // changed for the account, and the operator is told which rule refused, by name.
        if (invitation.EmailRefusalCode is { } refusal && refusal != AdminInvitationRefusals.AccountExists && !invitation.UserProvisioned
            && invitation.SetPasswordUrl is null && refusal.StartsWith("INVITE_LINK_ROOT_", StringComparison.Ordinal))
        {
            return Response<TenantAdminUserDto>.Fail(
                "The invitation link cannot be sent from this server: AuthService:FrontendBaseUrl is not a public https address.",
                422, refusal);
        }

        user.Status = TenantAdminUserStatus.Invited;
        user.InvitedAt = now;
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

        var dto = TenantAdminUserSupport.ToDto(user);

        // SMTP-off path is still a successful provisioning. In Development only, surface the administrator's
        // set-password link so the operator can pass it on (mirrors the tenant-side Users invite dev-fallback). BL-454
        // slice 2 stage D: there is no temporary password any more — nowhere, in no environment.
        if (!invitation.InvitationEmailSent && _environment.IsDevelopment())
        {
            dto = dto with
            {
                LoginUrl = invitation.SetPasswordUrl ?? invitation.LoginUrl,
                TemporaryPassword = null,
                EmailSent = false
            };
        }

        return Response<TenantAdminUserDto>.Success(dto);
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
