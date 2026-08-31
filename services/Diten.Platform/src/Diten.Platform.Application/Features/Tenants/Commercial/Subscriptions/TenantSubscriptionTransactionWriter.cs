using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;

public sealed class TenantSubscriptionTransactionWriter
{
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITenantSubscriptionRepository _subscriptions;
    private readonly ITenantRegistryRepository _tenants;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ICurrentUserContext _currentUser;

    public TenantSubscriptionTransactionWriter(IPlatformTransactionExecutor transactions,
        ITenantSubscriptionRepository subscriptions, ITenantRegistryRepository tenants,
        ISubscriptionPlanRepository plans, IEntitlementStateVersionRepository versions,
        ITransactionalIntegrationEventWriter events, ITransactionalAuditOutboxWriter audit,
        ICurrentUserContext currentUser)
    {
        _transactions = transactions;
        _subscriptions = subscriptions;
        _tenants = tenants;
        _plans = plans;
        _versions = versions;
        _events = events;
        _audit = audit;
        _currentUser = currentUser;
    }

    public Task<Response<Guid>> CreateAsync(TenantSubscription subscription, Tenant tenant,
        SubscriptionPlan plan, string mutation, AuditOperation operation,
        Func<IPlatformTransactionSession, TenantSubscription, SubscriptionPlan, CancellationToken, Task<Response<NoContent>>>? participant,
        CancellationToken ct) => ExecuteAsync(async (session, transactionCt) =>
        {
            await _subscriptions.CreateAsync(session, subscription, transactionCt);
            ApplyTenantSnapshot(tenant, subscription, plan, false, mutation, DateTimeOffset.UtcNow);
            await _tenants.UpdateAsync(session, tenant, transactionCt);
            if (participant is not null)
            {
                var response = await participant(session, subscription, plan, transactionCt);
                if (!response.IsSuccessful) throw new SubscriptionMutationRejectedException(response.Errors, response.StatusCode);
            }
            await WriteIntentsAsync(session, subscription, null, mutation, operation, transactionCt);
            return Response<Guid>.Success(subscription.Id, 201);
        }, ct);

    public Task<Response<NoContent>> UpdateAsync(TenantSubscription subscription, byte[]? expectedRowVersion,
        Guid previousPlanId, string previousStatus, string mutation, AuditOperation operation,
        bool markTenantActive,
        Func<IPlatformTransactionSession, TenantSubscription, SubscriptionPlan, CancellationToken, Task<Response<NoContent>>>? participant,
        CancellationToken ct) => ExecuteAsync(async (session, transactionCt) =>
        {
            await _subscriptions.UpdateAsync(session, subscription, expectedRowVersion, transactionCt);
            var tenant = await _tenants.GetByIdAsync(subscription.TenantId, transactionCt)
                ?? throw new SubscriptionMutationRejectedException(["Tenant not found."], 404);
            var plan = await _plans.GetByIdAsync(subscription.PlanId, transactionCt);
            if (plan is null) throw new SubscriptionMutationRejectedException(["Subscription plan not found."], 404);
            ApplyTenantSnapshot(tenant, subscription, plan, markTenantActive, mutation, DateTimeOffset.UtcNow);
            await _tenants.UpdateAsync(session, tenant, transactionCt);
            if (participant is not null)
            {
                var response = await participant(session, subscription, plan, transactionCt);
                if (!response.IsSuccessful) throw new SubscriptionMutationRejectedException(response.Errors, response.StatusCode);
            }
            await WriteIntentsAsync(session, subscription, (previousPlanId, previousStatus), mutation, operation, transactionCt);
            return Response<NoContent>.Success(204);
        }, ct);

    private async Task WriteIntentsAsync(IPlatformTransactionSession session, TenantSubscription subscription,
        (Guid PlanId, string Status)? previous, string mutation, AuditOperation operation, CancellationToken ct)
    {
        await _versions.IncrementSubscriptionSelectionVersionAsync(session, subscription.TenantId, ct);
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await _events.EnqueueAsync(session, new TenantSubscriptionChangedV1(eventId, now,
                subscription.TenantId, correlationId,
                _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId,
                previous?.PlanId ?? subscription.PlanId, subscription.PlanId,
                previous?.Status ?? "None", subscription.Status.ToString()),
            new EventPublishOptions { EventId = eventId, CorrelationId = correlationId,
                TenantId = subscription.TenantId, Producer = "Diten.Platform", OccurredAtUtc = now }, ct);
        var inserted = await _audit.TryEnqueueAsync(session, new AuditOutboxWriteRequest
        {
            TenantId = subscription.TenantId,
            CorrelationId = correlationId,
            IdempotencyKey = $"tenant-subscription:{mutation}:{subscription.Id:N}:{eventId:N}",
            RequestType = mutation,
            Operation = operation,
            EntityType = "TenantSubscription",
            EntityId = subscription.Id,
            Payload = new Dictionary<string, object?> { ["Outcome"] = "Succeeded", ["Status"] = subscription.Status.ToString() }
        }, ct);
        if (!inserted) throw new PlatformTransactionUnavailableException("Transactional subscription audit intent was not inserted.");
    }

    private static void ApplyTenantSnapshot(Tenant tenant, TenantSubscription subscription, SubscriptionPlan? plan,
        bool markTenantActive, string mutation, DateTimeOffset now)
    {
        tenant.PlanId = subscription.PlanId;
        tenant.PlanCode = plan?.Code;
        tenant.PlanName = plan?.Name;
        tenant.SubscriptionStatus = subscription.Status;
        tenant.TrialStartDateUtc = subscription.TrialStartDateUtc;
        tenant.TrialEndDateUtc = subscription.TrialEndDateUtc;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = subscription.UpdatedBy;
        tenant.ActivityTimeline.Add(new TenantActivityEvent { EventType = $"tenant_subscription.{mutation}",
            Message = $"Tenant subscription mutation {mutation} completed.", Actor = subscription.UpdatedBy, At = now });
        if (!markTenantActive) return;
        if (tenant.Status == TenantStatus.Provisioning)
        {
            tenant.Status = TenantStatus.Active;
            tenant.ActivatedAt ??= now;
        }
        tenant.ProvisionedAt ??= now;
        foreach (var step in tenant.ProvisioningSteps.Where(x =>
                     string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.Status, "InProgress", StringComparison.OrdinalIgnoreCase)))
        {
            step.Status = "Completed";
            step.CompletedAt ??= now;
        }
        tenant.ProvisioningStatus = "Completed";
    }

    private async Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken ct)
    {
        try { return await _transactions.ExecuteAsync(body, ct); }
        catch (TenantSubscriptionConcurrencyException)
        {
            if (typeof(T) == typeof(Response<Guid>))
                return (T)(object)Response<Guid>.Fail("Tenant subscription was modified by another process.", 409);
            return (T)(object)Response<NoContent>.Fail("Tenant subscription was modified by another process.", 409);
        }
        catch (SubscriptionMutationRejectedException ex)
        {
            if (typeof(T) == typeof(Response<Guid>)) return (T)(object)Response<Guid>.Fail(ex.Errors, ex.StatusCode);
            return (T)(object)Response<NoContent>.Fail(ex.Errors, ex.StatusCode);
        }
    }

    private sealed class SubscriptionMutationRejectedException(IReadOnlyList<string> errors, int statusCode) : Exception
    {
        public IReadOnlyList<string> Errors { get; } = errors;
        public int StatusCode { get; } = statusCode;
    }
}
