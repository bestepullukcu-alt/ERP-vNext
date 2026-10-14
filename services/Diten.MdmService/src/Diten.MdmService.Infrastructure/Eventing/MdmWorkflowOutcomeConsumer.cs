using System.Text.Json;
using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.Platform.Application.Contracts.Eventing;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Infrastructure.Eventing;

/// <summary>
/// WP-VW-W4 (W4-BE-b1) — applies MOD-0023's <c>platform.workflow.instance.completed.v1</c> to approvable MDM records (the
/// CrmService <c>ClaimWorkflowOutcomeConsumer</c> copy): broker-independent <see cref="ConsumeAsync"/>, inbox idempotency
/// (<c>mdm_event_inbox</c>, one row per EventId), routed by ObjectType (<c>mdm.*</c> only — another module's workflow is
/// not ours).
/// <para><b>Trust.</b> Platform publishes UNSIGNED today, so there is no signature to verify. An event is applied only when
/// it BINDS to state MDM wrote itself: the tenant, the object id and the workflow instance id must match the record's OPEN
/// round (the instance id MDM stored when MOD-0023 accepted the start) — the applier refuses everything else. The envelope
/// tenant and the payload tenant must agree. Reconcile-on-read (caller's token) remains the authoritative fallback.</para>
/// </summary>
public sealed class MdmWorkflowOutcomeConsumer : IConsumer<EventTransportMessage>
{
    public const string CompletedEventName = "platform.workflow.instance.completed.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkflowApprovalOutcomeApplier _applier;
    private readonly IMdmEventInboxRepository _inbox;
    private readonly ILogger<MdmWorkflowOutcomeConsumer> _logger;

    public MdmWorkflowOutcomeConsumer(WorkflowApprovalOutcomeApplier applier, IMdmEventInboxRepository inbox,
        ILogger<MdmWorkflowOutcomeConsumer> logger)
    {
        _applier = applier;
        _inbox = inbox;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context) => ConsumeAsync(context.Message, context.CancellationToken);

    /// <summary>Returns the apply result (null when the event was not processed: not ours / invalid / a duplicate).</summary>
    public async Task<WorkflowApplyResult?> ConsumeAsync(EventTransportMessage message, CancellationToken ct = default)
    {
        if (!string.Equals(message.EventName, CompletedEventName, StringComparison.Ordinal))
        {
            return null; // not ours
        }

        var payload = Deserialize(message);
        if (payload is null || !WorkflowApprovalOutcomeApplier.Handles(payload.ObjectType) || !_applier.CanApply(payload.ObjectType))
        {
            return null; // another module's workflow (or an MDM kind this build does not know)
        }

        var tenantId = payload.TenantId != Guid.Empty ? payload.TenantId : message.TenantId ?? Guid.Empty;
        var objectIdParsed = Guid.TryParse(payload.ObjectId, out var objectId);
        if (tenantId == Guid.Empty || (message.TenantId is { } envelopeTenant && envelopeTenant != tenantId)
            || !objectIdParsed || payload.WorkflowInstanceId == Guid.Empty
            || !WorkflowApprovalOutcomes.IsValid(payload.Outcome))
        {
            _logger.LogWarning("mdm.approval.event_rejected EventId={EventId} reason=shape_or_tenant", message.EventId);
            return null;
        }

        if (!await _inbox.TryInsertAsync(message.EventId, message.EventName, tenantId, ct))
        {
            _logger.LogInformation("mdm.approval.event_duplicate EventId={EventId}", message.EventId);
            return null;
        }

        var result = await _applier.ApplyAsync(tenantId, payload.ObjectType!, objectId, payload.WorkflowInstanceId,
            payload.Outcome!, payload.CompletedBy, payload.ReasonCode, payload.CompletedAt ?? message.OccurredAtUtc, ct);
        _logger.LogInformation(
            "mdm.approval.event_processed EventId={EventId} ObjectType={ObjectType} ObjectId={ObjectId} "
            + "WorkflowInstanceId={InstanceId} Outcome={Outcome} Result={Result}",
            message.EventId, payload.ObjectType, objectId, payload.WorkflowInstanceId, payload.Outcome, result);
        return result;
    }

    private CompletedPayload? Deserialize(EventTransportMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<CompletedPayload>(message.PayloadJson, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "mdm.approval.event_payload_invalid EventId={EventId}", message.EventId);
            return null;
        }
    }

    // Minimal projection of Platform's WorkflowInstanceCompletedV1.
    private sealed record CompletedPayload(
        Guid TenantId,
        Guid WorkflowInstanceId,
        string? ObjectType,
        string? ObjectId,
        string? Outcome,
        DateTimeOffset? CompletedAt,
        string? CompletedBy,
        string? ReasonCode);
}
