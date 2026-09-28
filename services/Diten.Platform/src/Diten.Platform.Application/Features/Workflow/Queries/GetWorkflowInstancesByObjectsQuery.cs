using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

/// <summary>
/// WP-CL-BE-3 — batch status read for cross-service reconciliation: every instance of up to
/// <see cref="MaxObjectIds"/> objects of one type, newest first per object. Tenant scoped.
/// </summary>
public sealed record GetWorkflowInstancesByObjectsQuery(
    string? ObjectType,
    IReadOnlyList<string>? ObjectIds,
    string CorrelationId) : IRequest<Response<IReadOnlyList<WorkflowObjectInstancesDto>>>
{
    public const int MaxObjectIds = 100;
}
