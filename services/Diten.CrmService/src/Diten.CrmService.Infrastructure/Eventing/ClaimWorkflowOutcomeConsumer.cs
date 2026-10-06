using System.Text.Json;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Application.Features.Knowledge.Regulatory;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.Platform.Application.Contracts.Eventing;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Eventing;

/// <summary>
/// WP-CL-BE-4 — applies MOD-0023's <c>platform.workflow.instance.completed.v1</c> to claims and claim country versions.
/// The AuthService <c>EntitlementSyncConsumer</c> pattern: broker-independent <see cref="ConsumeAsync"/>, inbox
/// idempotency (<c>crm_event_inbox</c>, one row per EventId).
/// <para><b>Trust.</b> Platform publishes UNSIGNED today (EmptyTrustedTransportMetadataProvider), so there is no
/// signature to verify. Until it signs, an event is applied only when it BINDS to state CRM wrote itself: the tenant,
/// the object id and the workflow instance id must match the record's OPEN review round (the instance id CRM stored
/// when MOD-0023 accepted the start). Anything else is ignored and logged. Reconcile-on-read (caller's token) remains
/// the authoritative fallback.</para>
/// <para>WP-KP-2 — routed by ObjectType on the SAME inbox: <c>crm.claim</c> / <c>crm.claim-country-version</c> → the
/// claim applier (unchanged); <c>crm.knowledge-path-revision</c> → the knowledge path revision applier.</para>
/// <para>WP-KP-5a — <c>crm.safety-text</c> / <c>crm.country-legal-profile</c> → the regulatory text applier (an added
/// route; the claim and path routes are unchanged).</para>
/// </summary>
public sealed class ClaimWorkflowOutcomeConsumer : IConsumer<EventTransportMessage>
{
    public const string CompletedEventName = "platform.workflow.instance.completed.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ClaimReviewOutcomeApplier _applier;
    private readonly ICrmEventInboxRepository _inbox;
    private readonly ILogger<ClaimWorkflowOutcomeConsumer> _logger;
    private readonly KnowledgePathRevisionOutcomeApplier? _pathApplier;
    private readonly RegulatoryTextOutcomeApplier? _regulatoryApplier;

    public ClaimWorkflowOutcomeConsumer(ClaimReviewOutcomeApplier applier, ICrmEventInboxRepository inbox,
        ILogger<ClaimWorkflowOutcomeConsumer> logger, KnowledgePathRevisionOutcomeApplier? pathApplier = null,
        RegulatoryTextOutcomeApplier? regulatoryApplier = null)
    {
        _applier = applier;
        _inbox = inbox;
        _logger = logger;
        _pathApplier = pathApplier;
        _regulatoryApplier = regulatoryApplier;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context) => ConsumeAsync(context.Message, context.CancellationToken);

    public async Task ConsumeAsync(EventTransportMessage message, CancellationToken ct = default)
    {
        if (!string.Equals(message.EventName, CompletedEventName, StringComparison.Ordinal))
        {
            return; // not ours
        }

        var payload = Deserialize(message);
        var isPath = payload?.ObjectType == KnowledgePathReviewRules.ObjectType && _pathApplier is not null;
        var isRegulatory = RegulatoryTextOutcomeApplier.Handles(payload?.ObjectType) && _regulatoryApplier is not null;
        if (payload is null
            || (!isPath && !isRegulatory
                && payload.ObjectType is not (ClaimReviewRules.ClaimObjectType or ClaimReviewRules.CountryVersionObjectType)))
        {
            return; // another module's workflow
        }

        var tenantId = payload.TenantId != Guid.Empty ? payload.TenantId : message.TenantId ?? Guid.Empty;
        var objectIdParsed = Guid.TryParse(payload.ObjectId, out var objectId);
        if (tenantId == Guid.Empty || (message.TenantId is { } envelopeTenant && envelopeTenant != tenantId)
            || !objectIdParsed || payload.WorkflowInstanceId == Guid.Empty
            || !ClaimReviewOutcomes.IsValid(payload.Outcome))
        {
            _logger.LogWarning("claims.review.event_rejected EventId={EventId} reason=shape_or_tenant", message.EventId);
            return;
        }

        if (!await _inbox.TryInsertAsync(message.EventId, message.EventName, tenantId, ct))
        {
            _logger.LogInformation("claims.review.event_duplicate EventId={EventId}", message.EventId);
            return;
        }

        var completedAt = payload.CompletedAt ?? message.OccurredAtUtc;
        var result = isPath
            ? await _pathApplier!.ApplyAsync(tenantId, objectId, payload.WorkflowInstanceId, payload.Outcome!,
                payload.CompletedBy, payload.ReasonCode, completedAt, ct)
            : isRegulatory
                ? await _regulatoryApplier!.ApplyAsync(tenantId, payload.ObjectType!, objectId, payload.WorkflowInstanceId,
                    payload.Outcome!, payload.CompletedBy, payload.ReasonCode, completedAt, ct)
            : await _applier.ApplyAsync(tenantId, payload.ObjectType!, objectId, payload.WorkflowInstanceId,
                payload.Outcome!, payload.CompletedBy, payload.ReasonCode, completedAt, ct);
        _logger.LogInformation(
            "claims.review.event_processed EventId={EventId} ObjectType={ObjectType} ObjectId={ObjectId} "
            + "WorkflowInstanceId={InstanceId} Outcome={Outcome} Result={Result}",
            message.EventId, payload.ObjectType, objectId, payload.WorkflowInstanceId, payload.Outcome, result);
    }

    private CompletedPayload? Deserialize(EventTransportMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<CompletedPayload>(message.PayloadJson, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "claims.review.event_payload_invalid EventId={EventId}", message.EventId);
            return null;
        }
    }

    // Minimal projection of WorkflowInstanceCompletedV1 (WP-CL-BE-3 consumer guide).
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
