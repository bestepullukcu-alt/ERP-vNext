using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;

public sealed class ReactivateTenantSubscriptionCommandHandler : IRequestHandler<ReactivateTenantSubscriptionCommand, Response<NoContent>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ICurrentUserContext _currentUser;

    public ReactivateTenantSubscriptionCommandHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ICurrentUserContext currentUser)
    {
        _subscriptionRepository = subscriptionRepository;
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(ReactivateTenantSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByTenantIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription == null)
        {
            return Response<NoContent>.Fail("Tenant subscription not found.", 404);
        }

        if (!TenantSubscriptionLifecycle.CanReactivate(subscription.Status))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {subscription.Status} to Active.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        subscription.Status = TenantSubscriptionStatus.Active;
        subscription.ActivatedAtUtc = now;
        subscription.SuspendedAtUtc = null;
        subscription.UpdatedBy = _currentUser.ActorName;
        TenantSubscriptionLifecycle.AddHistory(subscription, "reactivated", request.Request.Reason, _currentUser.ActorName, now);

        try
        {
            await _subscriptionRepository.UpdateAsync(subscription, request.Request.RowVersion, ct);
        }
        catch (TenantSubscriptionConcurrencyException)
        {
            return Response<NoContent>.Fail("Tenant subscription was modified by another process.", 409);
        }

        return await TenantSubscriptionCommandSupport.UpdateTenantSnapshotAsync(subscription, _tenantRepository, _planRepository, _currentUser, ct);
    }
}
