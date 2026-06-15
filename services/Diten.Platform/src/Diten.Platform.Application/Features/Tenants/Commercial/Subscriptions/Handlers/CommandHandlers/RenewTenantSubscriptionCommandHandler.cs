using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;

public sealed class RenewTenantSubscriptionCommandHandler : IRequestHandler<RenewTenantSubscriptionCommand, Response<NoContent>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IEventBus _eventBus;

    public RenewTenantSubscriptionCommandHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ICurrentUserContext currentUser,
        IEventBus eventBus)
    {
        _subscriptionRepository = subscriptionRepository;
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _currentUser = currentUser;
        _eventBus = eventBus;
    }

    public async Task<Response<NoContent>> Handle(RenewTenantSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByTenantIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription == null)
        {
            return Response<NoContent>.Fail("Tenant subscription not found.", 404);
        }

        if (!TenantSubscriptionLifecycle.CanRenew(subscription.Status))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {subscription.Status} to Active.", 400);
        }

        var previousPlanId = subscription.PlanId;
        var previousStatus = subscription.Status.ToString();
        var now = DateTimeOffset.UtcNow;
        var nextStart = subscription.CurrentPeriodEndUtc ?? now;
        if (request.Request.NewPeriodEndUtc <= nextStart)
        {
            return Response<NoContent>.Fail("New period end date must be after the current period end date.", 400);
        }

        subscription.CurrentPeriodStartUtc = nextStart;
        subscription.CurrentPeriodEndUtc = request.Request.NewPeriodEndUtc;
        subscription.RenewedAtUtc = now;
        subscription.CancelAtPeriodEnd = false;
        subscription.CancellationReason = null;
        subscription.UpdatedBy = _currentUser.ActorName;
        TenantSubscriptionLifecycle.AddHistory(subscription, "renewed", null, _currentUser.ActorName, now);

        try
        {
            await _subscriptionRepository.UpdateAsync(subscription, request.Request.RowVersion, ct);
        }
        catch (TenantSubscriptionConcurrencyException)
        {
            return Response<NoContent>.Fail("Tenant subscription was modified by another process.", 409);
        }

        var snapshotResponse = await TenantSubscriptionCommandSupport.UpdateTenantSnapshotAsync(subscription, _tenantRepository, _planRepository, _currentUser, ct);
        if (!snapshotResponse.IsSuccessful)
        {
            return snapshotResponse;
        }

        await PublishChangedAsync(request.TenantId, previousPlanId, subscription.PlanId, previousStatus, subscription.Status.ToString(), now, ct);
        return snapshotResponse;
    }

    private Task PublishChangedAsync(
        Guid tenantId,
        Guid previousPlanId,
        Guid newPlanId,
        string previousStatus,
        string newStatus,
        DateTimeOffset occurredAtUtc,
        CancellationToken ct)
    {
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var actorId = _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId;

        return _eventBus.PublishAsync(
            new TenantSubscriptionChangedV1(
                eventId,
                occurredAtUtc,
                tenantId,
                correlationId,
                actorId,
                previousPlanId,
                newPlanId,
                previousStatus,
                newStatus),
            new EventPublishOptions
            {
                EventId = eventId,
                CorrelationId = correlationId,
                TenantId = tenantId,
                Producer = "Diten.Platform",
                OccurredAtUtc = occurredAtUtc
            },
            ct);
    }
}
