using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

/// <summary>
/// WP-CL-BE-3 — the reconciliation read a consumer (CRM) runs when it may have missed a completion event. Tenant
/// isolation comes from the tenant-scoped repository; an object with no instance in this tenant answers with an empty
/// list (never another tenant's data). Ordering is done here, in memory: DateTimeOffset is persisted as a BSON array,
/// so a server-side sort on it is not safe.
/// </summary>
public sealed class GetWorkflowInstancesByObjectsHandler
    : IRequestHandler<GetWorkflowInstancesByObjectsQuery, Response<IReadOnlyList<WorkflowObjectInstancesDto>>>
{
    private readonly IWorkflowInstanceRepository _instances;

    public GetWorkflowInstancesByObjectsHandler(IWorkflowInstanceRepository instances) => _instances = instances;

    public async Task<Response<IReadOnlyList<WorkflowObjectInstancesDto>>> Handle(
        GetWorkflowInstancesByObjectsQuery request,
        CancellationToken ct)
    {
        var objectType = request.ObjectType?.Trim();
        if (string.IsNullOrWhiteSpace(objectType))
        {
            return Response<IReadOnlyList<WorkflowObjectInstancesDto>>.Fail(
                "objectType is required.", 400, WorkflowReasonCodes.ValidationFailed, request.CorrelationId);
        }

        var objectIds = (request.ObjectIds ?? [])
            .SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (objectIds.Count == 0)
        {
            return Response<IReadOnlyList<WorkflowObjectInstancesDto>>.Fail(
                "At least one objectId is required.", 400, WorkflowReasonCodes.ValidationFailed, request.CorrelationId);
        }

        if (objectIds.Count > GetWorkflowInstancesByObjectsQuery.MaxObjectIds)
        {
            return Response<IReadOnlyList<WorkflowObjectInstancesDto>>.Fail(
                $"At most {GetWorkflowInstancesByObjectsQuery.MaxObjectIds} objectIds can be read at once.",
                400, WorkflowReasonCodes.WorkflowBatchLimitExceeded, request.CorrelationId);
        }

        var byObject = (await _instances.ListByObjectIdsAsync(objectType, objectIds, ct))
            .Where(x => x.DeletedAt is null)
            .GroupBy(x => x.ObjectId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var result = objectIds.Select(id => new WorkflowObjectInstancesDto(
                id,
                (byObject.TryGetValue(id, out var rows) ? rows : [])
                    .OrderByDescending(x => x.StartedAt.HasValue)
                    .ThenByDescending(x => x.StartedAt ?? DateTimeOffset.MinValue)
                    .ThenByDescending(x => x.CreatedAt)
                    .Select(x => new WorkflowInstanceStatusDto(
                        x.Id,
                        x.Status.ToString(),
                        WorkflowOutcomes.For(x.Status),
                        x.CurrentStage,
                        x.CurrentStep,
                        x.StartedAt,
                        x.CompletedAt))
                    .ToList()))
            .ToList();

        return Response<IReadOnlyList<WorkflowObjectInstancesDto>>.Success(result, correlationId: request.CorrelationId);
    }
}
