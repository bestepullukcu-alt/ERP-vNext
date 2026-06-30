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
    public async Task Subscription_changed_with_empty_pull_does_not_reconcile()
    {
        var sync = new FakeSync();
        var client = new FakeEntitlementClient([]); // Platform unreachable / no entitlements
        var consumer = Build(sync, new FakeInbox(firstDelivery: true), client);

        await consumer.ConsumeAsync(Message(TenantSubscriptionChangedV1.Name, TenantA, moduleCode: ""));

        Assert.Null(sync.Synced); // never strip grants on an empty pull
    }

    // ── harness ──

    private static EntitlementSyncConsumer Build(FakeSync sync, FakeInbox inbox, FakeEntitlementClient? client = null)
        => new(sync, client ?? new FakeEntitlementClient([]), inbox, NullLogger<EntitlementSyncConsumer>.Instance);

    private static EventTransportMessage Message(string eventName, Guid tenantId, string moduleCode)
    {
        var payloadJson = JsonSerializer.Serialize(new { tenantId, moduleCode });
        return new EventTransportMessage(
            Guid.NewGuid(), eventName, 1, Guid.NewGuid(), null, tenantId, "platform", DateTimeOffset.UtcNow, payloadJson);
    }

    private sealed class FakeSync : IEntitlementPermissionSyncService
    {
        public (Guid tenantId, string moduleCode)? Granted { get; private set; }
        public (Guid tenantId, string moduleCode)? Revoked { get; private set; }
        public (Guid tenantId, string[] codes)? Synced { get; private set; }

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
            return Task.CompletedTask;
        }

        public Task GrantModuleWithKeysAsync(Guid tenantId, string moduleCode, IReadOnlyCollection<string> permissionKeys, string actor, CancellationToken ct = default)
        {
            Granted = (tenantId, moduleCode);
            return Task.CompletedTask;
        }

        public Task SyncTenantModulesWithKeysAsync(Guid tenantId, IReadOnlyCollection<EntitledModulePermissionKeys> modules, string actor, CancellationToken ct = default)
        {
            Synced = (tenantId, modules.Select(m => m.ModuleCode).ToArray());
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEntitlementClient(IReadOnlyList<string> codes) : ITenantEntitlementClient
    {
        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(codes);

        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EntitledModulePermissionKeys>>(
                codes.Select(c => new EntitledModulePermissionKeys(c, Array.Empty<string>())).ToList());
    }

    private sealed class FakeInbox(bool firstDelivery) : IIntegrationEventInboxRepository
    {
        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(firstDelivery);
    }
}
