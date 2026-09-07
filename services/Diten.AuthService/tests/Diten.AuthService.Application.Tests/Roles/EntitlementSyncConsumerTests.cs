using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
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
        var consumer = Build(sync, new FakeInbox(firstDelivery: false)); // inbox says already-seen

        await consumer.ConsumeAsync(Message(TenantEntitlementAddedV1.Name, TenantA, "MDM"));

        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
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
    public async Task Failed_authoritative_mutation_is_not_marked_complete_and_same_event_can_retry()
    {
        var sync = new FakeSync { FailNextGrant = true };
        var inbox = new FakeInbox(firstDelivery: true);
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));
        var message = Message(TenantEntitlementAddedV1.Name, TenantA, "product-item-sku-master");

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(message));

        Assert.Equal(0, inbox.Attempts);
        Assert.Null(sync.Granted);

        await consumer.ConsumeAsync(message);

        Assert.Equal((TenantA, "product-item-sku-master"), sync.Granted);
        Assert.Equal(1, inbox.Attempts);
    }

    [Fact]
    public async Task Completed_event_id_replayed_for_another_tenant_fails_before_mutation()
    {
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var eventId = Guid.NewGuid();
        var sync = new FakeSync();
        var inbox = new FakeInbox(firstDelivery: true);
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));

        await consumer.ConsumeAsync(Message(
            TenantEntitlementAddedV1.Name,
            TenantA,
            "product-item-sku-master",
            eventId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(Message(
            TenantEntitlementAddedV1.Name,
            tenantB,
            "product-item-sku-master",
            eventId)));

        Assert.Equal((TenantA, "product-item-sku-master"), sync.Granted);
        Assert.Equal(1, inbox.Attempts);
    }

    [Fact]
    public async Task Concurrent_first_deliveries_with_same_event_id_bind_before_mutation()
    {
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var eventId = Guid.NewGuid();
        var mutationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sync = new FakeSync
        {
            GrantEntered = mutationEntered,
            GrantContinue = continueMutation
        };
        var inbox = new FakeInbox(firstDelivery: true);
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));

        var first = consumer.ConsumeAsync(Message(
            TenantEntitlementAddedV1.Name,
            TenantA,
            "product-item-sku-master",
            eventId));
        await mutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(Message(
            TenantEntitlementAddedV1.Name,
            tenantB,
            "product-item-sku-master",
            eventId)));

        continueMutation.SetResult();
        await first;

        Assert.Equal((TenantA, "product-item-sku-master"), sync.Granted);
        Assert.Equal(1, inbox.Attempts);
    }

    [Fact]
    public async Task Completed_event_id_reused_for_another_event_name_fails_before_mutation()
    {
        var eventId = Guid.NewGuid();
        var sync = new FakeSync();
        var consumer = Build(
            sync,
            new FakeInbox(firstDelivery: true),
            new FakeEntitlementClient(["product-item-sku-master"]));

        await consumer.ConsumeAsync(Message(
            TenantEntitlementAddedV1.Name,
            TenantA,
            "product-item-sku-master",
            eventId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(Message(
            TenantEntitlementDisabledV1.Name,
            TenantA,
            "product-item-sku-master",
            eventId)));

        Assert.Equal((TenantA, "product-item-sku-master"), sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Fact]
    public async Task Payload_and_envelope_tenant_mismatch_fails_before_read_claim_or_mutation()
    {
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var sync = new FakeSync();
        var inbox = new FakeInbox(firstDelivery: true);
        var client = new FakeEntitlementClient(["product-item-sku-master"]);
        var consumer = Build(sync, inbox, client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(Message(
            TenantEntitlementAddedV1.Name,
            tenantB,
            "product-item-sku-master",
            envelopeTenantId: TenantA)));

        Assert.Equal(0, client.ReadAttempts);
        Assert.Equal(0, inbox.ClaimAttempts);
        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
    }

    [Fact]
    public async Task Claimed_result_without_fencing_token_fails_before_mutation()
    {
        var sync = new FakeSync();
        var inbox = new FakeInbox(firstDelivery: true) { ReturnClaimWithoutToken = true };
        var consumer = Build(sync, inbox, new FakeEntitlementClient(["product-item-sku-master"]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(Message(
            TenantEntitlementAddedV1.Name,
            TenantA,
            "product-item-sku-master")));

        Assert.Null(sync.Granted);
        Assert.Null(sync.Revoked);
        Assert.Equal(0, inbox.Attempts);
    }

    // ── harness ──

    private static EntitlementSyncConsumer Build(FakeSync sync, FakeInbox inbox, FakeEntitlementClient? client = null)
        => new(sync, client ?? new FakeEntitlementClient(["MDM"]), inbox, NullLogger<EntitlementSyncConsumer>.Instance);

    private static EventTransportMessage Message(
        string eventName,
        Guid tenantId,
        string moduleCode,
        Guid? eventId = null,
        Guid? envelopeTenantId = null)
    {
        var payloadJson = JsonSerializer.Serialize(new { tenantId, moduleCode });
        return new EventTransportMessage(
            eventId ?? Guid.NewGuid(), eventName, 1, Guid.NewGuid(), null, envelopeTenantId ?? tenantId, "platform", DateTimeOffset.UtcNow, payloadJson);
    }

    private sealed class FakeSync : IEntitlementPermissionSyncService
    {
        public (Guid tenantId, string moduleCode)? Granted { get; private set; }
        public (Guid tenantId, string moduleCode)? Revoked { get; private set; }
        public (Guid tenantId, string[] codes)? Synced { get; private set; }
        public int SyncCount { get; private set; }
        public bool FailNextGrant { get; set; }
        public TaskCompletionSource? GrantEntered { get; init; }
        public TaskCompletionSource? GrantContinue { get; init; }

        public Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
        {
            Granted = (tenantId, moduleCode);
            return Task.CompletedTask;
        }

        public Task RevokeModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
        {
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
            if (FailNextGrant)
            {
                FailNextGrant = false;
                throw new InvalidOperationException("Injected authoritative grant failure.");
            }

            GrantEntered?.SetResult();
            if (GrantContinue is not null)
            {
                await GrantContinue.Task.WaitAsync(ct);
            }

            Granted = (tenantId, moduleCode);
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
        public int ReadAttempts { get; private set; }

        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(codes);

        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EntitledModulePermissionKeys>>(
                codes.Select(c => new EntitledModulePermissionKeys(c, Array.Empty<string>())).ToList());

        public Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
        {
            ReadAttempts++;
            return Task.FromResult(isAuthoritative
                ? TenantEntitlementReadResult.Confirmed(
                    codes.Select(c => new EntitledModulePermissionKeys(c, Array.Empty<string>())).ToList())
                : TenantEntitlementReadResult.Unavailable());
        }
    }

    private sealed class FakeInbox(bool firstDelivery) : IIntegrationEventInboxRepository
    {
        private readonly object _gate = new();
        private Guid? _tenantId = firstDelivery ? null : TenantA;
        private string? _eventName;
        private Guid? _claimId;
        private IntegrationEventInboxState _state = IntegrationEventInboxState.Completed;

        public int Attempts { get; private set; }
        public int ClaimAttempts { get; private set; }
        public bool ReturnClaimWithoutToken { get; init; }

        public Task<IntegrationEventClaim> TryClaimAsync(
            Guid eventId,
            string eventName,
            Guid tenantId,
            TimeSpan leaseDuration,
            CancellationToken ct = default)
        {
            lock (_gate)
            {
                ClaimAttempts++;
                if (ReturnClaimWithoutToken)
                {
                    return Task.FromResult(new IntegrationEventClaim(IntegrationEventClaimResult.Claimed));
                }
                if (!_tenantId.HasValue)
                {
                    _tenantId = tenantId;
                    _eventName = eventName;
                    _state = IntegrationEventInboxState.Processing;
                    _claimId = Guid.NewGuid();
                    return Task.FromResult(new IntegrationEventClaim(
                        IntegrationEventClaimResult.Claimed,
                        _claimId));
                }

                if (_tenantId.Value != tenantId)
                {
                    return Task.FromResult(new IntegrationEventClaim(IntegrationEventClaimResult.TenantMismatch));
                }

                if (_eventName is not null && !string.Equals(_eventName, eventName, StringComparison.Ordinal))
                {
                    return Task.FromResult(new IntegrationEventClaim(IntegrationEventClaimResult.IdentityMismatch));
                }

                return Task.FromResult(new IntegrationEventClaim(
                    _state == IntegrationEventInboxState.Completed
                        ? IntegrationEventClaimResult.Completed
                        : IntegrationEventClaimResult.Busy));
            }
        }

        public Task CompleteClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (_tenantId != tenantId || _claimId != claimId || _state != IntegrationEventInboxState.Processing)
                {
                    throw new InvalidOperationException("Claim cannot be completed.");
                }

                _state = IntegrationEventInboxState.Completed;
                _claimId = null;
                Attempts++;
                return Task.CompletedTask;
            }
        }

        public Task ReleaseClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (_tenantId == tenantId && _claimId == claimId && _state == IntegrationEventInboxState.Processing)
                {
                    _tenantId = null;
                    _eventName = null;
                    _claimId = null;
                }

                return Task.CompletedTask;
            }
        }

        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
        {
            lock (_gate)
            {
                Attempts++;
                var result = !_tenantId.HasValue;
                _tenantId ??= tenantId;
                _eventName ??= eventName;
                _state = IntegrationEventInboxState.Completed;
                return Task.FromResult(result);
            }
        }
    }
}
