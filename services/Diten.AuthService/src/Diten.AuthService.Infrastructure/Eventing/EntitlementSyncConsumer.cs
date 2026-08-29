using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
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

        var tenantId = message.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return; // no server-bound tenant context
        }

        if (payload.TenantId != Guid.Empty && payload.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"Integration event '{message.EventId}' payload tenant conflicts with its transport tenant.");
        }

        // Grant/Revoke target one module; Reconcile (subscription change) needs only the tenant.
        if (operation != EntitlementOperation.Reconcile && string.IsNullOrWhiteSpace(payload.ModuleCode))
        {
            return;
        }

        var inboxEntry = await _inbox.GetAsync(message.EventId, message.EventName, tenantId, ct);
        if (inboxEntry is null)
        {
            var reserved = await _inbox.TryInsertAsync(message.EventId, message.EventName, tenantId, ct);
            if (!reserved)
            {
                // EventId is globally unique. A failed exact reservation followed by no tenant-bound exact row is
                // therefore fact drift; reject it before any authoritative read or grant/revoke mutation.
                inboxEntry = await _inbox.GetAsync(message.EventId, message.EventName, tenantId, ct);
                if (inboxEntry is null)
                {
                    throw new InvalidOperationException(
                        $"Integration event '{message.EventId}' conflicts with previously recorded event facts.");
                }
            }
            else
            {
                inboxEntry = new IntegrationEventInboxEntry(
                    message.EventId,
                    message.EventName,
                    tenantId,
                    CompletionProtocolVersion: null);
            }
        }

        if (inboxEntry is not null)
        {
            EnsureExactInboxFacts(inboxEntry, message.EventName, tenantId);
            if (inboxEntry.CompletionProtocolVersion == ProcessedIntegrationEvent.CurrentCompletionProtocolVersion)
            {
                _logger.LogInformation(
                    "entitlement.sync.duplicate_ignored EventId={EventId} EventName={EventName} TenantId={TenantId}",
                    message.EventId, message.EventName, tenantId);
                return;
            }
        }

        // The inbox is completion-only. Authoritative reads and reconciliation happen before the atomic completion
        // upsert, so unavailable reads and partial repository failures remain replayable under the same EventId.
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

        await _inbox.MarkCompletedAsync(message.EventId, message.EventName, tenantId, ct);

        _logger.LogInformation(
            "entitlement.sync.applied EventId={EventId} EventName={EventName} TenantId={TenantId} ModuleCode={ModuleCode}",
            message.EventId, message.EventName, tenantId, payload.ModuleCode);
    }

    private static void EnsureExactInboxFacts(
        IntegrationEventInboxEntry existing,
        string eventName,
        Guid tenantId)
    {
        if (existing.TenantId != tenantId
            || !string.Equals(existing.EventName, eventName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Integration event '{existing.EventId}' conflicts with previously recorded event facts.");
        }
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
