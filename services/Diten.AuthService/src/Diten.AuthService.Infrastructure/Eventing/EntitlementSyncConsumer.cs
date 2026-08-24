using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Infrastructure.Eventing;

/// <summary>
/// Cross-service consumer that syncs a tenant's role permissions when Platform publishes
/// <see cref="TenantEntitlementAddedV1"/> / <see cref="TenantEntitlementEnabledV1"/> /
/// <see cref="TenantEntitlementDisabledV1"/>. Transport (MassTransit/RabbitMQ) is wired in DI and
/// gated by <see cref="AuthServiceEventingOptions.UseRabbitMq"/>; the dispatch logic in
/// <see cref="ConsumeAsync"/> is broker-independent and unit-tested. Real end-to-end delivery is
/// verified in S18.
/// </summary>
public sealed class EntitlementSyncConsumer : IConsumer<EventTransportMessage>
{
    public const string ConsumerName = nameof(EntitlementSyncConsumer);
    private const string Actor = "entitlement-sync";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEntitlementPermissionSyncService _sync;
    private readonly ITenantEntitlementClient _entitlementClient;
    private readonly IIntegrationEventInboxRepository _inbox;
    private readonly ILogger<EntitlementSyncConsumer> _logger;

    public EntitlementSyncConsumer(
        IEntitlementPermissionSyncService sync,
        ITenantEntitlementClient entitlementClient,
        IIntegrationEventInboxRepository inbox,
        ILogger<EntitlementSyncConsumer> logger)
    {
        _sync = sync;
        _entitlementClient = entitlementClient;
        _inbox = inbox;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context)
        => ConsumeAsync(context.Message, context.CancellationToken);

    // Broker-independent dispatch — directly unit-testable with a hand-built EventTransportMessage.
    public async Task ConsumeAsync(EventTransportMessage message, CancellationToken ct = default)
    {
        var operation = message.EventName switch
        {
            TenantEntitlementAddedV1.Name => EntitlementOperation.Grant,
            TenantEntitlementEnabledV1.Name => EntitlementOperation.Grant,
            TenantEntitlementDisabledV1.Name => EntitlementOperation.Revoke,
            TenantEntitlementExpiryUpdatedV1.Name => EntitlementOperation.Reconcile,
            TenantEntitlementOverrideRemovedV1.Name => EntitlementOperation.Reconcile,
            // FIX-2 — a plan/subscription change re-points the tenant's virtual (plan-derived) entitlement set,
            // which emits no per-module events. Pull the authoritative set and reconcile Module-grants.
            TenantSubscriptionChangedV1.Name => EntitlementOperation.Reconcile,
            _ => EntitlementOperation.Ignore
        };

        if (operation == EntitlementOperation.Ignore)
        {
            return; // not an entitlement event we handle
        }

        var payload = Deserialize(message);
        if (payload is null)
        {
            return; // malformed payload → fail-safe no-op
        }

        var tenantId = payload.TenantId != Guid.Empty
            ? payload.TenantId
            : message.TenantId ?? Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            return; // nothing actionable
        }

        // Grant/Revoke target one module; Reconcile (subscription change) needs only the tenant.
        if (operation != EntitlementOperation.Reconcile && string.IsNullOrWhiteSpace(payload.ModuleCode))
        {
            return;
        }

        // Idempotency — reuse the internal-events inbox; the sync operations are themselves idempotent,
        // so this primarily suppresses redundant work on re-delivery.
        TenantEntitlementReadResult? entitlementRead = null;
        if (operation is EntitlementOperation.Grant or EntitlementOperation.Reconcile)
        {
            entitlementRead = await _entitlementClient.ReadEntitledModulesWithPermissionKeysAsync(tenantId, ct);
            if (!entitlementRead.IsAuthoritative)
            {
                // No authoritative decision was made, so the same EventId must remain retryable.
                LogUnavailable(message, tenantId);
                return;
            }
        }

        var firstDelivery = await _inbox.TryInsertAsync(message.EventId, message.EventName, tenantId, ct);
        if (!firstDelivery)
        {
            _logger.LogInformation(
                "entitlement.sync.duplicate_ignored EventId={EventId} EventName={EventName} TenantId={TenantId}",
                message.EventId, message.EventName, tenantId);
            return;
        }

        switch (operation)
        {
            case EntitlementOperation.Grant:
                // Resolve the module's DECLARED catalog permission keys (namespace-agnostic) only from an
                // authoritative read. A confirmed result that omits the event module revokes that module's sourced
                // grants; an unavailable read returned before the inbox insert and remains retryable.
                var grantedModule = entitlementRead!.Modules
                    .FirstOrDefault(m => string.Equals(m.ModuleCode, payload.ModuleCode, StringComparison.OrdinalIgnoreCase));
                if (grantedModule is null)
                {
                    await _sync.RevokeModuleAsync(tenantId, payload.ModuleCode, Actor, ct);
                    break;
                }

                await _sync.GrantModuleWithKeysAsync(
                    tenantId, payload.ModuleCode, grantedModule.PermissionKeys, Actor, ct);
                break;
            case EntitlementOperation.Revoke:
                await _sync.RevokeModuleAsync(tenantId, payload.ModuleCode, Actor, ct);
                break;
            case EntitlementOperation.Reconcile:
                // Catalog-key-driven authoritative reconcile. Confirmed empty removes stale module grants; an
                // unavailable read returned before the inbox insert and performs no grant or revoke.
                await _sync.SyncTenantModulesWithKeysAsync(tenantId, entitlementRead!.Modules, Actor, ct);
                break;
        }

        _logger.LogInformation(
            "entitlement.sync.applied EventId={EventId} EventName={EventName} TenantId={TenantId} ModuleCode={ModuleCode}",
            message.EventId, message.EventName, tenantId, payload.ModuleCode);
    }

    private void LogUnavailable(EventTransportMessage message, Guid tenantId)
        => _logger.LogWarning(
            "entitlement.sync.skipped_unavailable EventId={EventId} EventName={EventName} TenantId={TenantId}",
            message.EventId, message.EventName, tenantId);

    private EntitlementPayload? Deserialize(EventTransportMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<EntitlementPayload>(message.PayloadJson, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "entitlement.sync.payload_invalid EventId={EventId} EventName={EventName}",
                message.EventId, message.EventName);
            return null;
        }
    }

    private enum EntitlementOperation
    {
        Ignore,
        Grant,
        Revoke,
        Reconcile
    }

    // Minimal projection of the entitlement events — only the fields the bridge needs.
    private sealed record EntitlementPayload(Guid TenantId, string ModuleCode);
}
