using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

/// <summary>
/// WP-WF-ENGINE-RECOVERY-01 FIX2 item 5 — CLOSES A HALF START THAT ITS OWN REPLAY CAN NO LONGER COMPLETE. A generic start that
/// never wrote its task is completed by its key's replay only with the same facts and by the same person (BL-549, FIX1 K1). When
/// the task's approval manager or reviewer was edited after a failed start, or somebody else submits, the replay is refused and
/// the round would stay gated until the abandoned-start sweep (off by default) closed it. This closes it at once, through the
/// engine's own taskless close (reason <see cref="WorkflowReasonCodes.WorkflowStartSuperseded"/>, a log row, the completion
/// event and — this being a command — an audit row under the person whose edit superseded it), so the edited request opens a
/// new round under a new key (SAP: a workflow restarted after its agent changed; Oracle: a withdrawn and resubmitted approval).
/// <para>Refused, with nothing written, for an instance that has ANY task, is not Active or is a trusted start. No age: an
/// instance with no task has nobody deciding it, and a first call still writing its task loses — its claim and task commit
/// together (FIX2 item 1), so it writes nothing under the closed instance.</para>
/// </summary>
public sealed record SupersedeWorkflowHalfStartCommand(
    Guid WorkflowInstanceId,
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
            ["EventName"] = "workflow.instance.supersede-half-start",
            ["ReasonCode"] = WorkflowReasonCodes.WorkflowStartSuperseded
        });
}
