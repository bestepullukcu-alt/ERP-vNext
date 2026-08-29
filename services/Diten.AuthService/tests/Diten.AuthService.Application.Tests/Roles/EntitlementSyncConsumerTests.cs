using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Roles;

// S3 — consumer dispatch logic (broker-independent; the MassTransit↔RabbitMQ binding is verified in
// S18). Exercises event routing, payload extraction, idempotency and fail-safe paths.
public sealed class EntitlementSyncConsumerTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Theory]
    [InlineData("tenant.entitlement.added.v1")]
    [InlineData("tenant.entitlement.enabled.v1")]
    public async Task Added_or_enabled_grants_the_module(string eventName)
    {
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true));

        await consumer.ConsumeAsync(Message(eventName, TenantA, "MDM"));

        Assert.Equal((TenantA, "MDM"), sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Fact]
    public async Task Disabled_revokes_the_module()
    {
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true));

        await consumer.ConsumeAsync(Message(TenantEntitlementDisabledV1.Name, TenantA, "MDM"));

        Assert.Equal((TenantA, "MDM"), sync.Revoked);
        Assert.Null(sync.Granted);
    }

    [Fact]
    public async Task Unknown_event_is_ignored()
    {
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true));

        await consumer.ConsumeAsync(Message("some.other.event.v1", TenantA, "MDM"));

        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Fact]
    public async Task Duplicate_delivery_is_skipped_via_inbox()
    {
        var sync = new FakeSync();
        var client = new FakeEntitlementClient(["MDM"]);
        var consumer = Build(sync, new FakeInbox(firstDelivery: false), client); // inbox says completed

        await consumer.ConsumeAsync(Message(TenantEntitlementAddedV1.Name, TenantA, "MDM"));

        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
        Assert.Equal(0, client.ReadCount);
    }

    [Fact]
    public async Task Malformed_payload_is_a_fail_safe_no_op()
    {
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true));

        var bad = new EventTransportMessage(Guid.NewGuid(), TenantEntitlementAddedV1.Name, 1,
            Guid.NewGuid(), null, TenantA, "platform", DateTimeOffset.UtcNow, "{ not valid json");

        await consumer.ConsumeAsync(bad);

        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Fact]
    public async Task Blank_module_code_is_a_no_op()
    {
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true));

        await consumer.ConsumeAsync(Message(TenantEntitlementAddedV1.Name, TenantA, ""));

        Assert.Null(sync.Granted);
    }

    [Fact]
    public async Task Subscription_changed_reconciles_against_the_pulled_entitled_set()
    {
        var sync = new FakeSync();
        var client = new FakeEntitlementClient(["goldenslim", "workflow"]);
        var consumer = Build(sync, new FakeInbox(firstDelivery: true), client);

        await consumer.ConsumeAsync(Message(TenantSubscriptionChangedV1.Name, TenantA, moduleCode: ""));

        Assert.Equal(TenantA, sync.Synced?.tenantId);
        Assert.Equal(new[] { "goldenslim", "workflow" }, sync.Synced?.codes);
        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Fact]
    public async Task Subscription_changed_with_confirmed_empty_pull_reconciles_authoritatively()
    {
        var sync = new FakeSync();
        var client = new FakeEntitlementClient([]); // Authoritative Platform result: no entitlements.
        var consumer = Build(sync, new FakeInbox(firstDelivery: true), client);

        await consumer.ConsumeAsync(Message(TenantSubscriptionChangedV1.Name, TenantA, moduleCode: ""));

        Assert.NotNull(sync.Synced);
        Assert.Empty(sync.Synced.Value.codes);
    }

    [Theory]
    [InlineData("tenant.entitlement.expiryupdated.v1")]
    [InlineData("tenant.entitlement.overrideremoved.v1")]
    public async Task State_change_reconciles_and_preserves_confirmed_fallback(string eventName)
    {
        var sync = new FakeSync();
        var consumer = Build(
            sync,
            new FakeInbox(firstDelivery: true),
            new FakeEntitlementClient(["product-item-sku-master"]));

        await consumer.ConsumeAsync(Message(eventName, TenantA, "product-item-sku-master"));

        Assert.Equal(TenantA, sync.Synced?.tenantId);
        Assert.Equal(new[] { "product-item-sku-master" }, sync.Synced?.codes);
        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Theory]
    [InlineData("tenant.entitlement.expiryupdated.v1")]
    [InlineData("tenant.entitlement.overrideremoved.v1")]
    public async Task State_change_with_confirmed_empty_reconciles_and_removes_stale_module_grants(string eventName)
    {
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true), new FakeEntitlementClient([]));

        await consumer.ConsumeAsync(Message(eventName, TenantA, "product-item-sku-master"));

        Assert.NotNull(sync.Synced);
        Assert.Empty(sync.Synced.Value.codes);
        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Theory]
    [InlineData("tenant.entitlement.expiryupdated.v1")]
    [InlineData("tenant.entitlement.overrideremoved.v1")]
    public async Task State_change_with_unavailable_read_is_not_consumed_and_same_event_can_retry(string eventName)
    {
        var sync = new FakeSync();
        var inbox = new FakeInbox(firstDelivery: true);
        var message = Message(eventName, TenantA, "product-item-sku-master");

        await Build(sync, inbox, new FakeEntitlementClient([], isAuthoritative: false)).ConsumeAsync(message);

        Assert.Null(sync.Synced);
        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
        Assert.Equal(0, inbox.Attempts);

        await Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"])).ConsumeAsync(message);

        Assert.Equal(1, sync.SyncCount);
        Assert.Equal(new[] { "product-item-sku-master" }, sync.Synced?.codes);
        Assert.Equal(1, inbox.Attempts);
    }

    [Theory]
    [InlineData("tenant.entitlement.expiryupdated.v1")]
    [InlineData("tenant.entitlement.overrideremoved.v1")]
    public async Task State_change_successful_replay_is_idempotent(string eventName)
    {
        var sync = new FakeSync();
        var inbox = new FakeInbox(firstDelivery: true);
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));
        var message = Message(eventName, TenantA, "product-item-sku-master");

        await consumer.ConsumeAsync(message);
        await consumer.ConsumeAsync(message);

        Assert.Equal(1, sync.SyncCount);
        Assert.Equal(1, inbox.Attempts);
    }

    [Fact]
    public async Task State_change_reconcile_uses_only_the_event_tenant()
    {
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true), new FakeEntitlementClient([]));

        await consumer.ConsumeAsync(Message(TenantEntitlementExpiryUpdatedV1.Name, tenantB, "product-item-sku-master"));

        Assert.Equal(tenantB, sync.Synced?.tenantId);
        Assert.NotEqual(TenantA, sync.Synced?.tenantId);
    }

    [Fact]
    public async Task Subscription_changed_with_unavailable_pull_mutates_neither_direction()
    {
        var sync = new FakeSync();
        var client = new FakeEntitlementClient([], isAuthoritative: false);
        var consumer = Build(sync, new FakeInbox(firstDelivery: true), client);

        await consumer.ConsumeAsync(Message(TenantSubscriptionChangedV1.Name, TenantA, moduleCode: ""));

        Assert.Null(sync.Synced);
        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Fact]
    public async Task Added_with_unavailable_pull_keeps_same_EventId_retryable()
    {
        var sync = new FakeSync();
        var inbox = new FakeInbox(firstDelivery: true);
        var message = Message(TenantEntitlementAddedV1.Name, TenantA, "product-item-sku-master");
        var unavailableConsumer = Build(
            sync, inbox, new FakeEntitlementClient([], isAuthoritative: false));

        await unavailableConsumer.ConsumeAsync(message);

        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
        Assert.Equal(0, inbox.Attempts);

        var retryConsumer = Build(
            sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));
        await retryConsumer.ConsumeAsync(message);

        Assert.Equal((TenantA, "product-item-sku-master"), sync.Granted);
        Assert.Null(sync.Revoked);
        Assert.Equal(1, inbox.Attempts);
    }

    [Fact]
    public async Task Added_with_confirmed_missing_module_revokes_only_that_module_source()
    {
        var sync = new FakeSync();
        var consumer = Build(sync, new FakeInbox(firstDelivery: true), new FakeEntitlementClient([]));

        await consumer.ConsumeAsync(Message(TenantEntitlementAddedV1.Name, TenantA, "product-item-sku-master"));

        Assert.Equal((TenantA, "product-item-sku-master"), sync.Revoked);
        Assert.Null(sync.Granted);
    }

    [Fact]
    public async Task Reconciliation_failure_does_not_complete_and_exact_replay_converges()
    {
        var sync = new FakeSync { FailNextGrant = true };
        var inbox = new FakeInbox(firstDelivery: true);
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));
        var message = Message(TenantEntitlementAddedV1.Name, TenantA, "product-item-sku-master");

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(message));

        Assert.Equal(1, sync.GrantCount);
        Assert.Equal(0, inbox.Attempts);
        Assert.Null((await inbox.GetAsync(message.EventId, message.EventName, TenantA))?.CompletionProtocolVersion);

        await consumer.ConsumeAsync(message);

        Assert.Equal(2, sync.GrantCount);
        Assert.Equal(1, inbox.Attempts);
        Assert.Equal(1, (await inbox.GetAsync(message.EventId, message.EventName, TenantA))?.CompletionProtocolVersion);
    }

    [Fact]
    public async Task Completion_failure_is_retryable_and_never_returns_false_success()
    {
        var sync = new FakeSync();
        var inbox = new FakeInbox(firstDelivery: true) { FailNextCompletion = true };
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));
        var message = Message(TenantEntitlementAddedV1.Name, TenantA, "product-item-sku-master");

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(message));

        Assert.Equal(1, sync.GrantCount);
        Assert.Null((await inbox.GetAsync(message.EventId, message.EventName, TenantA))?.CompletionProtocolVersion);

        await consumer.ConsumeAsync(message);

        Assert.Equal(2, sync.GrantCount);
        Assert.Equal(2, inbox.Attempts);
        Assert.Equal(1, (await inbox.GetAsync(message.EventId, message.EventName, TenantA))?.CompletionProtocolVersion);
    }

    [Fact]
    public async Task Legacy_unconfirmed_row_is_replayed_and_lazily_upgraded()
    {
        var eventId = Guid.NewGuid();
        var message = Message(
            TenantEntitlementDisabledV1.Name,
            TenantA,
            "product-item-sku-master",
            eventId);
        var inbox = new FakeInbox(new IntegrationEventInboxEntry(
            eventId,
            message.EventName,
            TenantA,
            CompletionProtocolVersion: null));
        var sync = new FakeSync();

        await Build(sync, inbox).ConsumeAsync(message);

        Assert.Equal(1, sync.RevokeCount);
        Assert.Equal(1, (await inbox.GetAsync(eventId, message.EventName, TenantA))?.CompletionProtocolVersion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Same_EventId_fact_drift_fails_closed_before_reconciliation(bool driftTenant)
    {
        var eventId = Guid.NewGuid();
        var message = Message(
            TenantEntitlementDisabledV1.Name,
            TenantA,
            "product-item-sku-master",
            eventId);
        var recorded = new IntegrationEventInboxEntry(
            eventId,
            driftTenant ? message.EventName : TenantEntitlementAddedV1.Name,
            driftTenant ? Guid.NewGuid() : TenantA,
            CompletionProtocolVersion: null);
        var sync = new FakeSync();
        var inbox = new FakeInbox(recorded);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Build(sync, inbox).ConsumeAsync(message));

        Assert.Equal(0, sync.RevokeCount);
        Assert.Equal(0, sync.GrantCount);
        Assert.Equal(0, inbox.Attempts);
    }

    [Fact]
    public async Task Payload_tenant_mismatch_fails_closed_before_reservation_or_reconciliation()
    {
        var payloadTenant = Guid.NewGuid();
        var message = Message(
            TenantEntitlementDisabledV1.Name,
            TenantA,
            "product-item-sku-master") with
        {
            PayloadJson = JsonSerializer.Serialize(new
            {
                tenantId = payloadTenant,
                moduleCode = "product-item-sku-master"
            })
        };
        var inbox = new FakeInbox(firstDelivery: true);
        var sync = new FakeSync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Build(sync, inbox).ConsumeAsync(message));

        Assert.Equal(0, sync.RevokeCount);
        Assert.Equal(0, sync.GrantCount);
        Assert.Equal(0, inbox.ReservationAttempts);
        Assert.Equal(0, inbox.Attempts);
    }

    [Fact]
    public async Task Cancellation_during_reconciliation_propagates_without_completion()
    {
        var sync = new FakeSync { CancelNextRevoke = true };
        var inbox = new FakeInbox(firstDelivery: true);
        var message = Message(TenantEntitlementDisabledV1.Name, TenantA, "product-item-sku-master");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(sync, inbox).ConsumeAsync(message));

        Assert.Equal(1, sync.RevokeCount);
        Assert.Equal(0, inbox.Attempts);
        Assert.Null((await inbox.GetAsync(message.EventId, message.EventName, TenantA))?.CompletionProtocolVersion);
    }

    [Fact]
    public async Task Concurrent_duplicate_is_at_least_once_until_completed_then_suppressed()
    {
        var sync = new FakeSync(expectedConcurrentGrants: 2);
        var inbox = new FakeInbox(firstDelivery: true);
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));
        var message = Message(TenantEntitlementAddedV1.Name, TenantA, "product-item-sku-master");

        await Task.WhenAll(consumer.ConsumeAsync(message), consumer.ConsumeAsync(message));

        Assert.Equal(2, sync.GrantCount);
        Assert.Equal(1, (await inbox.GetAsync(message.EventId, message.EventName, TenantA))?.CompletionProtocolVersion);

        await consumer.ConsumeAsync(message);

        Assert.Equal(2, sync.GrantCount);
    }

    // ── harness ──

    private static EntitlementSyncConsumer Build(FakeSync sync, FakeInbox inbox, FakeEntitlementClient? client = null)
        => new(sync, client ?? new FakeEntitlementClient(["MDM"]), inbox, NullLogger<EntitlementSyncConsumer>.Instance);

    private static EventTransportMessage Message(
        string eventName,
        Guid tenantId,
        string moduleCode,
        Guid? eventId = null)
    {
        var payloadJson = JsonSerializer.Serialize(new { tenantId, moduleCode });
        return new EventTransportMessage(
            eventId ?? Guid.NewGuid(), eventName, 1, Guid.NewGuid(), null, tenantId, "platform", DateTimeOffset.UtcNow, payloadJson);
    }

    private sealed class FakeSync : IEntitlementPermissionSyncService
    {
        private readonly int _expectedConcurrentGrants;
        private readonly TaskCompletionSource _concurrentGrants = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _grantCount;
        private int _revokeCount;

        public FakeSync(int expectedConcurrentGrants = 0)
        {
            _expectedConcurrentGrants = expectedConcurrentGrants;
        }

        public (Guid tenantId, string moduleCode)? Granted { get; private set; }
        public (Guid tenantId, string moduleCode)? Revoked { get; private set; }
        public (Guid tenantId, string[] codes)? Synced { get; private set; }
        public int SyncCount { get; private set; }
        public int GrantCount => Volatile.Read(ref _grantCount);
        public int RevokeCount => Volatile.Read(ref _revokeCount);
        public bool FailNextGrant { get; set; }
        public bool CancelNextRevoke { get; set; }

        public Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
        {
            Granted = (tenantId, moduleCode);
            return Task.CompletedTask;
        }

        public Task RevokeModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _revokeCount);
            if (CancelNextRevoke)
            {
                CancelNextRevoke = false;
                throw new OperationCanceledException(ct);
            }

            Revoked = (tenantId, moduleCode);
            return Task.CompletedTask;
        }

        public Task SyncTenantModulesAsync(Guid tenantId, IReadOnlyCollection<string> entitledModuleCodes, string actor, CancellationToken ct = default)
        {
            Synced = (tenantId, entitledModuleCodes.ToArray());
            SyncCount++;
            return Task.CompletedTask;
        }

        public async Task GrantModuleWithKeysAsync(Guid tenantId, string moduleCode, IReadOnlyCollection<string> permissionKeys, string actor, CancellationToken ct = default)
        {
            var grantCount = Interlocked.Increment(ref _grantCount);
            if (FailNextGrant)
            {
                FailNextGrant = false;
                throw new InvalidOperationException("injected grant failure");
            }

            Granted = (tenantId, moduleCode);
            if (_expectedConcurrentGrants > 0)
            {
                if (grantCount >= _expectedConcurrentGrants)
                {
                    _concurrentGrants.TrySetResult();
                }

                await _concurrentGrants.Task.WaitAsync(ct);
            }
        }

        public Task SyncTenantModulesWithKeysAsync(Guid tenantId, IReadOnlyCollection<EntitledModulePermissionKeys> modules, string actor, CancellationToken ct = default)
        {
            Synced = (tenantId, modules.Select(m => m.ModuleCode).ToArray());
            SyncCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEntitlementClient(IReadOnlyList<string> codes, bool isAuthoritative = true) : ITenantEntitlementClient
    {
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(codes);

        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EntitledModulePermissionKeys>>(
                codes.Select(c => new EntitledModulePermissionKeys(c, Array.Empty<string>())).ToList());

        public Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
        {
            ReadCount++;
            return Task.FromResult(isAuthoritative
                ? TenantEntitlementReadResult.Confirmed(
                    codes.Select(c => new EntitledModulePermissionKeys(c, Array.Empty<string>())).ToList())
                : TenantEntitlementReadResult.Unavailable());
        }
    }

    private sealed class FakeInbox : IIntegrationEventInboxRepository
    {
        private readonly object _gate = new();
        private IntegrationEventInboxEntry? _entry;

        public FakeInbox(bool firstDelivery)
        {
            if (!firstDelivery)
            {
                _entry = new IntegrationEventInboxEntry(
                    Guid.Empty,
                    TenantEntitlementAddedV1.Name,
                    TenantA,
                    CompletionProtocolVersion: 1);
            }
        }

        public FakeInbox(IntegrationEventInboxEntry entry)
        {
            _entry = entry;
        }

        public int Attempts { get; private set; }
        public int ReservationAttempts { get; private set; }
        public bool FailNextCompletion { get; set; }

        public Task<IntegrationEventInboxEntry?> GetAsync(
            Guid eventId,
            string eventName,
            Guid tenantId,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_entry is not null && _entry.EventId == Guid.Empty)
                {
                    _entry = _entry with { EventId = eventId };
                }

                return Task.FromResult(
                    _entry is not null
                    && _entry.EventId == eventId
                    && _entry.TenantId == tenantId
                    && string.Equals(_entry.EventName, eventName, StringComparison.Ordinal)
                        ? _entry
                        : null);
            }
        }

        public Task MarkCompletedAsync(
            Guid eventId,
            string eventName,
            Guid tenantId,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                Attempts++;
                if (FailNextCompletion)
                {
                    FailNextCompletion = false;
                    throw new InvalidOperationException("injected completion failure");
                }

                if (_entry is not null
                    && (_entry.EventId != eventId
                        || _entry.TenantId != tenantId
                        || !string.Equals(_entry.EventName, eventName, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("event facts conflict");
                }

                _entry = new IntegrationEventInboxEntry(eventId, eventName, tenantId, CompletionProtocolVersion: 1);
                return Task.CompletedTask;
            }
        }

        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ReservationAttempts++;
                if (_entry is not null)
                {
                    return Task.FromResult(false);
                }

                _entry = new IntegrationEventInboxEntry(eventId, eventName, tenantId, CompletionProtocolVersion: null);
                return Task.FromResult(true);
            }
        }
    }
}
