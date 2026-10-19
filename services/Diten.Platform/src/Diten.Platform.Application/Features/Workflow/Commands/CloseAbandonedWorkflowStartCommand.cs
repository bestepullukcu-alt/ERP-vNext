using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

/// <summary>
/// BL-549 — closes ONE generic start that never wrote its task (an Active instance nobody can decide), through the engine's
/// own terminal write: a named reason, a transition log row and the completion event, and — this being a command — an audit
/// row (<see cref="IAuditableCommand"/>; no principal → the System actor). Sent by the abandoned-start sweep only.
/// <para>Refused, with nothing written, for an instance that has any task the task store lists (open or decided), that is younger than
/// <paramref name="MinimumAge"/>, that is not Active or that is a trusted start (those complete through their coordinator).
/// Idempotent: an instance this recovery already closed answers <see cref="CloseAbandonedWorkflowStartResult.AlreadyClosed"/>.</para>
/// </summary>
public sealed record CloseAbandonedWorkflowStartCommand(
    Guid WorkflowInstanceId,
    TimeSpan MinimumAge,
    string CorrelationId) : IRequest<Response<CloseAbandonedWorkflowStartResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration,
        AuditOperation.Execute,
        "WorkflowInstance",
        WorkflowInstanceId,
        SourceModule: "MOD-0023",
        Metadata: new Dictionary<string, object?>
        {
            ["EventName"] = "workflow.instance.close-abandoned-start",
            ["ActorId"] = CloseAbandonedWorkflowStartResult.SystemActor,
            ["ReasonCode"] = WorkflowReasonCodes.WorkflowStartAbandoned
        });
}

public sealed record CloseAbandonedWorkflowStartResult(Guid WorkflowInstanceId, Guid TransitionLogId, bool AlreadyClosed)
{
    /// <summary>The actor every recovery close is written under (log, audit row, completion event).</summary>
    public const string SystemActor = "system:workflow-start-recovery";
}
