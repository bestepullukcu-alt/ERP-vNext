using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Application.Features.Workflow.Events;

/// <summary>
/// WP-CL-BE-3 — a workflow instance reached a terminal state (approved / rejected / cancelled / timed-out). Written to
/// the transactional outbox in the SAME Platform transaction as the terminal transition, so the state change and the
/// event commit together or not at all. Exactly one per instance: the event id is derived from the instance id, and
/// the outbox refuses a second intent with the same id.
/// <para>Payload = identifiers, codes and times only. No comment text, no evidence text, no names (PII-free);
/// <see cref="CompletedBy"/> is the acting principal id (null for the system timeout).</para>
/// </summary>
public sealed record WorkflowInstanceCompletedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid TenantId,
    Guid CorrelationId,
    Guid WorkflowInstanceId,
    string? TemplateCode,
    Guid? TemplateVersionId,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string Outcome,
    DateTimeOffset CompletedAt,
    string? CompletedBy,
    string FinalStageCode,
    string FinalStepCode,
    string? ReasonCode) : IIntegrationEvent
{
    public const string Name = "platform.workflow.instance.completed.v1";
    public const int Version = 1;

    public string EventName => Name;
    public int EventVersion => Version;
}
