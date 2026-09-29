using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

/// <param name="AllowEscalated">B3 — also cancel an ESCALATED approval task. Never bound from a request body: only the
/// owning module's in-process path sets it (the timesheet withdraw), because withdrawing one's own object is the one
/// case where an escalated approval may still be called off. The public cancel endpoint always sends false.</param>
public sealed record CancelWorkflowTaskCommand(
    Guid TaskId,
    CancelWorkflowTaskRequest Request,
    string CorrelationId,
    bool AllowEscalated = false) : IRequest<Response<WorkflowTaskTransitionResponse>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration,
        AuditOperation.Execute,
        "ApprovalTask",
        TaskId,
        SourceModule: "MOD-0023",
        Metadata: new Dictionary<string, object?>
        {
            ["EventName"] = "workflow.task.cancel",
            ["ActorId"] = Request.ActorId,
            ["ReasonCode"] = Request.ReasonCode
        });
}
