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
    string CorrelationId,
    // MOD-0023 §17.7/3 — soft-deleted rows are hidden unless asked for; then they come back flagged. Trailing, optional.
    bool IncludeDeleted = false) : IRequest<Response<IReadOnlyList<WorkflowObjectInstancesDto>>>
{
    public const int MaxObjectIds = 100;
}
