using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

/// <summary>
/// MOD-0023 / FAZ B WorkCenter inbox foundation — the actionable workflow tasks for the CURRENT user: tasks
/// in a non-terminal (actionable) status where the caller is a candidate in the task's RuntimeAssignmentSnapshot
/// (or the resolved assignee). Tenant-scoped. Permission: platform.workflow.instances.view.
/// </summary>
public sealed record GetMyWorkflowTasksQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<WorkflowTaskDto>>>;
