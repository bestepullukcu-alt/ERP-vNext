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

/// <summary>WP-PLATFORM-AUDIT-INTX-01 FIX1 — the stable codes of a refused subscription write.</summary>
public static class TenantSubscriptionRefusalCodes
{
    /// <summary>The tenant already has a live subscription (any of <see cref="TenantSubscriptionStatuses.Current"/>).</summary>
    public const string AlreadyCurrent = "SUBSCRIPTION_ALREADY_CURRENT";

    /// <summary>Another request changed this tenant's subscription at the same moment; the screen reloads.</summary>
    public const string Stale = "SUBSCRIPTION_STALE";
}

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
    private readonly Diten.Platform.Common.Observability.ICorrelationContext? _correlation;

    public TenantSubscriptionTransactionWriter(IPlatformTransactionExecutor transactions,
        ITenantSubscriptionRepository subscriptions, ITenantRegistryRepository tenants,
        ISubscriptionPlanRepository plans, IEntitlementStateVersionRepository versions,
        ITransactionalIntegrationEventWriter events, ITransactionalAuditOutboxWriter audit,
        ICurrentUserContext currentUser,
        Diten.Platform.Common.Observability.ICorrelationContext? correlation = null)
    {
        _correlation = correlation;
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
            /*
             * FIX1 — ONE live subscription per tenant. The unique index (TenantId, Status) only stops two of the SAME
             * status; it lets a Trialing and an Active live side by side. It is not changed here (that is a drop and
             * rebuild on live data — proposed in the FIX1 report). Two guards instead, both inside this transaction:
             *   - the check below, read again at the start of the transaction body (the committed state, not the
             *     request's earlier read): a request that gets here after the other committed is refused with its code;
             *   - the tenant document: both requests write it, so of two that overlap exactly one commits — the other
             *     meets a write conflict and is answered as stale (WriteTenantAsync), never as a server error.
             */
            if (await _subscriptions.HasCurrentAsync(session, subscription.TenantId, null, transactionCt))
            {
                throw new SubscriptionMutationRejectedException(
                    ["Tenant already has a current subscription."], 409, TenantSubscriptionRefusalCodes.AlreadyCurrent);
            }

            await _subscriptions.CreateAsync(session, subscription, transactionCt);
            // INTX FIX2 — the tenant as THIS transaction sees it; the copy the request read before the transaction
            // began could carry another request's state from before its commit and write it back.
            var current = await _tenants.GetByIdAsync(session, tenant.Id, transactionCt)
                ?? throw new SubscriptionMutationRejectedException(["Tenant not found."], 404);
            ApplyTenantSnapshot(current, subscription, plan, false, mutation, DateTimeOffset.UtcNow);
            await WriteTenantAsync(session, current, transactionCt);
            if (participant is not null)
            {
                var response = await participant(session, subscription, plan, transactionCt);
                if (!response.IsSuccessful) throw new SubscriptionMutationRejectedException(response.Errors, response.StatusCode, response.ReasonCode);
            }
            await WriteIntentsAsync(session, subscription, null, null, mutation, operation, transactionCt);
            return Response<Guid>.Success(subscription.Id, 201);
        }, ct);

    public Task<Response<NoContent>> UpdateAsync(TenantSubscription subscription, byte[]? expectedRowVersion,
        Guid previousPlanId, string previousStatus, string mutation, AuditOperation operation,
        bool markTenantActive,
        Func<IPlatformTransactionSession, TenantSubscription, SubscriptionPlan, CancellationToken, Task<Response<NoContent>>>? participant,
        CancellationToken ct) => ExecuteAsync(async (session, transactionCt) =>
        {
            // What is stored BEFORE this write — the audit record's before-state (statuses and dates). FIX3 — read in the
            // transaction: the before-state is the one this write replaces, not one read beside it.
            var stored = await _subscriptions.GetByIdAsync(session, subscription.Id, transactionCt);
            var auditBefore = stored is null || ReferenceEquals(stored, subscription) ? null : StateOf(stored);
            await _subscriptions.UpdateAsync(session, subscription, expectedRowVersion, transactionCt);
            var tenant = await _tenants.GetByIdAsync(session, subscription.TenantId, transactionCt)
                ?? throw new SubscriptionMutationRejectedException(["Tenant not found."], 404);
            var plan = await PlanInTransactionAsync(session, subscription.PlanId, transactionCt);
            if (plan is null) throw new SubscriptionMutationRejectedException(["Subscription plan not found."], 404);
            ApplyTenantSnapshot(tenant, subscription, plan, markTenantActive, mutation, DateTimeOffset.UtcNow);
            await WriteTenantAsync(session, tenant, transactionCt);
            if (participant is not null)
            {
                var response = await participant(session, subscription, plan, transactionCt);
                if (!response.IsSuccessful) throw new SubscriptionMutationRejectedException(response.Errors, response.StatusCode, response.ReasonCode);
            }
            await WriteIntentsAsync(session, subscription, (previousPlanId, previousStatus), auditBefore, mutation, operation, transactionCt);
            return Response<NoContent>.Success(204);
        }, ct);

    /// <summary>INTX FIX3 — the plan as THIS transaction sees it. A plan repository that cannot read in a transaction
    /// refuses (fail closed), the way the session members of the other repositories do.</summary>
    private Task<SubscriptionPlan?> PlanInTransactionAsync(IPlatformTransactionSession session, Guid planId, CancellationToken ct) =>
        _plans is ITransactionalSubscriptionPlanRepository transactional
            ? transactional.GetByIdAsync(session, planId, ct)
            : throw new PlatformTransactionUnavailableException("The subscription plan repository does not implement transaction-bound reads.");

    /// <summary>
    /// The tenant document is the one record every subscription write of a tenant touches. A write conflict on it is
    /// another subscription write of the same tenant in flight: answered as stale (409 with its code). Retrying
    /// immediately — what the executor does with a transient conflict — ran out of attempts before the other request
    /// committed and ended as a 500.
    /// </summary>
    private async Task WriteTenantAsync(IPlatformTransactionSession session, Tenant tenant, CancellationToken ct)
    {
        try
        {
            await _tenants.UpdateAsync(session, tenant, ct);
        }
        catch (MongoDB.Driver.MongoException exception) when (IsWriteConflict(exception))
        {
            throw new TenantSubscriptionConcurrencyException();
        }
    }

    private static bool IsWriteConflict(MongoDB.Driver.MongoException exception) => exception switch
    {
        MongoDB.Driver.MongoWriteException write => write.WriteError?.Code == 112,
        MongoDB.Driver.MongoCommandException command => command.Code == 112,
        _ => false
    };

    private async Task WriteIntentsAsync(IPlatformTransactionSession session, TenantSubscription subscription,
        (Guid PlanId, string Status)? previous, IReadOnlyDictionary<string, object?>? storedBefore,
        string mutation, AuditOperation operation, CancellationToken ct)
    {
        await _versions.IncrementSubscriptionSelectionVersionAsync(session, subscription.TenantId, ct);
        var eventId = Guid.NewGuid();
        // INTX FIX2 — the integration event and the audit record carry ONE correlation: the request's (the canonical
        // audit writer resolves the same value), or one fresh id for both when no request is in scope.
        var correlationId = AuditCorrelation.Resolve(_correlation?.CorrelationId, Guid.NewGuid());
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
            // WP-PLATFORM-AUDIT-INTX-01 — an INTENT; the canonical payload is built at the writer's entry.
            Intent = new TransactionOwnedAuditIntent
            {
                Category = AuditCategory.SubscriptionBilling,
                TargetTenantId = subscription.TenantId,
                BeforeState = storedBefore ?? (previous is { } p
                    ? new Dictionary<string, object?> { ["Status"] = p.Status, ["PlanId"] = p.PlanId.ToString("D") }
                    : null),
                AfterState = StateOf(subscription),
                SourceModule = "subscription-billing"
            }
        }, ct);
        if (!inserted) throw new PlatformTransactionUnavailableException("Transactional subscription audit intent was not inserted.");
    }

    /// <summary>What an auditor reads of a subscription: status, plan and its dates (ISO-8601 text). No free text.</summary>
    private static IReadOnlyDictionary<string, object?> StateOf(TenantSubscription subscription)
    {
        static string? Iso(DateTimeOffset? value) =>
            value?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        return new Dictionary<string, object?>
        {
            ["Status"] = subscription.Status.ToString(),
            ["PlanId"] = subscription.PlanId.ToString("D"),
            ["TrialEndDateUtc"] = Iso(subscription.TrialEndDateUtc),
            ["CurrentPeriodStartUtc"] = Iso(subscription.CurrentPeriodStartUtc),
            ["CurrentPeriodEndUtc"] = Iso(subscription.CurrentPeriodEndUtc),
            ["CancelAtPeriodEnd"] = subscription.CancelAtPeriodEnd
        };
    }

    // Internal (not private) so its "everything pending is now done" sweep is measured directly (BL-454 stage D FIX2 (2)).
    internal static void ApplyTenantSnapshot(Tenant tenant, TenantSubscription subscription, SubscriptionPlan? plan,
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
                     x.Key != TenantProvisioningStep.AdminInvitationKey && // BL-454 stage D FIX2 (2)
                     (string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.Status, "InProgress", StringComparison.OrdinalIgnoreCase))))
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
                return (T)(object)Response<Guid>.Fail("Tenant subscription was modified by another process.", 409, TenantSubscriptionRefusalCodes.Stale);
            return (T)(object)Response<NoContent>.Fail("Tenant subscription was modified by another process.", 409, TenantSubscriptionRefusalCodes.Stale);
        }
        catch (SubscriptionMutationRejectedException ex)
        {
            if (typeof(T) == typeof(Response<Guid>)) return (T)(object)Response<Guid>.Fail(ex.Errors, ex.StatusCode, ex.ReasonCode);
            return (T)(object)Response<NoContent>.Fail(ex.Errors, ex.StatusCode, ex.ReasonCode);
        }
    }

    private sealed class SubscriptionMutationRejectedException(IReadOnlyList<string> errors, int statusCode, string? reasonCode = null) : Exception
    {
        public IReadOnlyList<string> Errors { get; } = errors;
        public int StatusCode { get; } = statusCode;
        public string? ReasonCode { get; } = reasonCode;
    }
}
