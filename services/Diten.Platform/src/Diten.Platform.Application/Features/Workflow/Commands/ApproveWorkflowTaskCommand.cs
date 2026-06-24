using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record ApproveWorkflowTaskCommand(
    Guid TaskId,
    ApproveWorkflowTaskRequest Request,
    string CorrelationId) : IRequest<Response<WorkflowTaskTransitionResponse>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration,
        AuditOperation.Execute,
        "ApprovalTask",
        TaskId,
        SourceModule: "MOD-0023",
        Metadata: new Dictionary<string, object?>
        {
            ["EventName"] = "workflow.task.approve",
            ["ActorId"] = Request.ActorId,
            ["ReasonCode"] = Request.ReasonCode
        });
}
