using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class InviteTenantAdminUserCommandHandler : IRequestHandler<InviteTenantAdminUserCommand, Response<TenantAdminUserDto>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAdminUserInvitationService _invitationService;
    private readonly IQuotaService _quotaService;
    private readonly ILogger<InviteTenantAdminUserCommandHandler> _logger;

    public InviteTenantAdminUserCommandHandler(
        ITenantRegistryRepository repository,
        ICurrentUserContext currentUser,
        IAdminUserInvitationService invitationService,
        IQuotaService quotaService,
        ILogger<InviteTenantAdminUserCommandHandler> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _invitationService = invitationService;
        _quotaService = quotaService;
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
        var reservedQuotaThisAttempt = false;

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
                else
                {
                    reservedQuotaThisAttempt = true;
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
            invitation = await _invitationService.InviteAsync(tenant, user, cancellationToken);
        }
        catch
        {
            if (reservedQuotaThisAttempt)
            {
                await ReleaseReservedQuotaAsync(tenant.Id, operationId, sourceReference, correlationId, CancellationToken.None);
            }

            return Response<TenantAdminUserDto>.Fail("Tenant admin invitation could not be completed.", 502);
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
            if (reservedQuotaThisAttempt)
            {
                await ReleaseReservedQuotaAsync(tenant.Id, operationId, sourceReference, correlationId, CancellationToken.None);
            }

            _logger.LogError(
                ex,
                "Tenant admin invitation state update failed. TenantId={TenantId} AdminUserId={AdminUserId}",
                tenant.Id,
                user.Id);
            return Response<TenantAdminUserDto>.Fail("Tenant admin invitation state could not be saved.", 502);
        }

        return Response<TenantAdminUserDto>.Success(TenantAdminUserSupport.ToDto(user));
    }

    private async Task ReleaseReservedQuotaAsync(
        Guid tenantId,
        string operationId,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _quotaService.ReleaseAsync(new ReleaseQuotaRequest(
                tenantId,
                QuotaKeys.UsersMax,
                1,
                "TenantAdminUserInviteRollback",
                $"{operationId}:rollback",
                sourceReference,
                "Tenant admin invitation failed after users quota reservation.",
                _currentUser.ActorName,
                correlationId), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Tenant admin users quota rollback failed. TenantId={TenantId} OperationId={OperationId}",
                tenantId,
                operationId);
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
