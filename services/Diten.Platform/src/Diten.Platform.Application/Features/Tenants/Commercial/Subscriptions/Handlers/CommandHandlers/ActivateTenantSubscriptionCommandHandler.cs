using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;

public sealed class ActivateTenantSubscriptionCommandHandler : IRequestHandler<ActivateTenantSubscriptionCommand, Response<NoContent>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IQuotaService _quotaService;
    private readonly TenantSubscriptionTransactionWriter _writer;

    public ActivateTenantSubscriptionCommandHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ICurrentUserContext currentUser,
        IQuotaService quotaService,
        TenantSubscriptionTransactionWriter writer)
    {
        _subscriptionRepository = subscriptionRepository;
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _currentUser = currentUser;
        _quotaService = quotaService;
        _writer = writer;
    }

    public async Task<Response<NoContent>> Handle(ActivateTenantSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByTenantIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription == null)
        {
            return Response<NoContent>.Fail("Tenant subscription not found.", 404);
        }

        if (!TenantSubscriptionLifecycle.CanActivate(subscription.Status))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {subscription.Status} to Active.", 400);
        }

        if (request.Request.CurrentPeriodEndUtc <= request.Request.CurrentPeriodStartUtc)
        {
            return Response<NoContent>.Fail("Current period end date must be after the start date.", 400);
        }

        var previousPlanId = subscription.PlanId;
        var previousStatus = subscription.Status.ToString();
        var now = DateTimeOffset.UtcNow;
        subscription.Status = TenantSubscriptionStatus.Active;
        subscription.CurrentPeriodStartUtc = request.Request.CurrentPeriodStartUtc;
        subscription.CurrentPeriodEndUtc = request.Request.CurrentPeriodEndUtc;
        subscription.ActivatedAtUtc = now;
        subscription.UpdatedBy = _currentUser.ActorName;
        TenantSubscriptionLifecycle.AddHistory(subscription, "activated", null, _currentUser.ActorName, now);

        return await _writer.UpdateAsync(subscription, request.Request.RowVersion,
            previousPlanId, previousStatus,
            nameof(ActivateTenantSubscriptionCommand), AuditOperation.Activate, true,
            async (session, current, plan, transactionCt) =>
            {
                var quota = await _quotaService.InitializeSubscriptionQuotasAsync(session, current, plan, false,
                    "SubscriptionActivation", "Tenant subscription activated; quota usage initialized.",
                    _currentUser.ActorName, Guid.NewGuid().ToString("N"), transactionCt);
                return quota.IsSuccessful ? Response<NoContent>.Success(204) : Response<NoContent>.Fail(quota.Errors, quota.StatusCode);
            }, ct);
    }
}
