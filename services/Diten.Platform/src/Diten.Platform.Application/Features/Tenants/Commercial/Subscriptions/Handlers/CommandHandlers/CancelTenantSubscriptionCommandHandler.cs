using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;

public sealed class CancelTenantSubscriptionCommandHandler : IRequestHandler<CancelTenantSubscriptionCommand, Response<NoContent>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly TenantSubscriptionTransactionWriter _writer;

    public CancelTenantSubscriptionCommandHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ICurrentUserContext currentUser,
        TenantSubscriptionTransactionWriter writer)
    {
        _subscriptionRepository = subscriptionRepository;
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _currentUser = currentUser;
        _writer = writer;
    }

    public async Task<Response<NoContent>> Handle(CancelTenantSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByTenantIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription == null)
        {
            return Response<NoContent>.Fail("Tenant subscription not found.", 404);
        }

        if (!TenantSubscriptionLifecycle.CanCancel(subscription.Status))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {subscription.Status} to Cancelled.", 400);
        }

        var previousPlanId = subscription.PlanId;
        var previousStatus = subscription.Status.ToString();
        var reason = request.Request.CancellationReason.Trim();
        var cancelAtPeriodEnd = request.Request.CancelAtPeriodEnd && subscription.Status == TenantSubscriptionStatus.Active;
        if (subscription.CancelAtPeriodEnd == cancelAtPeriodEnd &&
            string.Equals(subscription.CancellationReason, reason, StringComparison.Ordinal))
        {
            return Response<NoContent>.Success(204);
        }
        var now = DateTimeOffset.UtcNow;
        subscription.CancellationReason = reason;
        subscription.CancelAtPeriodEnd = cancelAtPeriodEnd;
        if (!subscription.CancelAtPeriodEnd)
        {
            subscription.Status = TenantSubscriptionStatus.Cancelled;
            subscription.CancelledAtUtc = now;
        }

        subscription.UpdatedBy = _currentUser.ActorName;
        TenantSubscriptionLifecycle.AddHistory(subscription, subscription.CancelAtPeriodEnd ? "cancel_at_period_end" : "cancelled", reason, _currentUser.ActorName, now);

        return await _writer.UpdateAsync(subscription, request.Request.RowVersion, previousPlanId, previousStatus,
            nameof(CancelTenantSubscriptionCommand), AuditOperation.LifecycleTransition, false, null, ct);
    }
}
