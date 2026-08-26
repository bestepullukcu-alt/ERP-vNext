using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>DCP-005 slice 1 — the read side of the task-type catalogue.</summary>
public sealed class GetTaskTypeListHandler
    : IRequestHandler<GetTaskTypeListQuery, Response<IReadOnlyList<TaskTypeDto>>>
{
    private readonly ITaskTypeRepository _types;

    public GetTaskTypeListHandler(ITaskTypeRepository types) => _types = types;

    public async Task<Response<IReadOnlyList<TaskTypeDto>>> Handle(
        GetTaskTypeListQuery query, CancellationToken ct)
    {
        // Retired types included: a type switched off could otherwise never be switched back on.
        var types = await _types.ListAllAsync(ct);
        return Response<IReadOnlyList<TaskTypeDto>>.Success(
            types.Where(t => t.DeletedAt is null).Select(TaskTypeMapping.ToDto).ToList(),
            200, query.CorrelationId);
    }
}

/// <summary>
/// The types a NEW task may be given.
///
/// <para>Guarded by <c>Read</c>, not by the management permission: a person who can create a task has to be able
/// to choose its type. What they cannot do is mint one — that separation is what QA's control statement rests
/// on.</para>
/// </summary>
public sealed class GetActiveTaskTypesHandler
    : IRequestHandler<GetActiveTaskTypesQuery, Response<IReadOnlyList<TaskTypeDto>>>
{
    private readonly ITaskTypeRepository _types;

    public GetActiveTaskTypesHandler(ITaskTypeRepository types) => _types = types;

    public async Task<Response<IReadOnlyList<TaskTypeDto>>> Handle(
        GetActiveTaskTypesQuery query, CancellationToken ct)
    {
        var types = await _types.ListActiveAsync(ct);
        return Response<IReadOnlyList<TaskTypeDto>>.Success(
            types.Select(TaskTypeMapping.ToDto).ToList(), 200, query.CorrelationId);
    }
}

public sealed class GetTaskTypeByIdHandler : IRequestHandler<GetTaskTypeByIdQuery, Response<TaskTypeDto>>
{
    private readonly ITaskTypeRepository _types;

    public GetTaskTypeByIdHandler(ITaskTypeRepository types) => _types = types;

    public async Task<Response<TaskTypeDto>> Handle(GetTaskTypeByIdQuery query, CancellationToken ct)
    {
        var type = await _types.GetByIdAsync(query.Id, ct);
        return type is null || type.DeletedAt is not null
            ? Response<TaskTypeDto>.Fail("Task type not found.", 404, TaskReasonCodes.NotFound, query.CorrelationId)
            : Response<TaskTypeDto>.Success(TaskTypeMapping.ToDto(type), 200, query.CorrelationId);
    }
}

/// <summary>One mapping, so the list and the single read cannot drift apart.</summary>
internal static class TaskTypeMapping
{
    public static TaskTypeDto ToDto(TaskType type) => new(
        type.Id,
        type.Code,
        type.Name,
        type.Description,
        type.RecordClass,
        type.GqmsDomain,
        type.FunctionCode,
        type.IsQualityEvent,
        type.GroupDocuments,
        type.LocalDocuments.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase),
        type.IsActive);
}
