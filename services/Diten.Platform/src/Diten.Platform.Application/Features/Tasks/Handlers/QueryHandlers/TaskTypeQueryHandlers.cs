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

/// <summary>
/// DCP-005 slice 3 — a task type's governing documents, resolved against the CURRENT register (§6.4).
///
/// <para><b>Empty is not one answer, it is three</b>, and the three are told apart because they mean different
/// things to the person choosing:</para>
/// <list type="bullet">
///   <item>the type names no document at all — nothing is missing, this kind of work is not governed;</item>
///   <item>the type names documents the register does not contain — the register is behind, or the type is;</item>
///   <item>the type names documents the register refuses to link — the document exists and cannot be cited.</item>
/// </list>
///
/// <para>MEASURED against the counterparty's own seed (2026-08-26): of 31 types, <b>15</b> have no citable
/// governing document — 1 names nothing (GEN-ADMIN), 7 name UIDs absent from the register, 7 name UIDs the
/// register blocks. Two more (DEV-GMP, DEV-GDP) name one citable and one blocked, so their suggestion is
/// PARTIAL rather than empty. A screen that drew a bare empty box would be wrong about all of them.</para>
/// </summary>
public sealed class GetTaskTypeGoverningDocumentsHandler
    : IRequestHandler<GetTaskTypeGoverningDocumentsQuery, Response<TaskTypeGoverningDocumentsDto>>
{
    private readonly ITaskTypeRepository _types;
    private readonly IDocumentReferenceListRepository _lists;

    public GetTaskTypeGoverningDocumentsHandler(
        ITaskTypeRepository types, IDocumentReferenceListRepository lists)
    {
        _types = types;
        _lists = lists;
    }

    public async Task<Response<TaskTypeGoverningDocumentsDto>> Handle(
        GetTaskTypeGoverningDocumentsQuery query, CancellationToken ct)
    {
        var type = await _types.GetByIdAsync(query.TaskTypeId, ct);
        if (type is null || type.DeletedAt is not null)
        {
            return Response<TaskTypeGoverningDocumentsDto>.Fail(
                "Task type not found.", 404, TaskReasonCodes.NotFound, query.CorrelationId);
        }

        // §6.4 — two layers, never a cross product: the group layer always, plus THIS organisation's local layer.
        var named = new List<string>(type.GroupDocuments);
        if (!string.IsNullOrWhiteSpace(query.OrganizationCode)
            && type.LocalDocuments.TryGetValue(query.OrganizationCode!, out var local))
        {
            named.AddRange(local);
        }

        named = named
            .Select(u => (u ?? string.Empty).Trim())
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var current = await _lists.GetLatestVersionAsync(ct);
        if (named.Count == 0 || current is null)
        {
            /*
             * ⚠ With no register imported, every named UID is UNRESOLVED rather than silently dropped. The
             * reader is then told "the register does not list these", which is true, instead of "this type is
             * not governed", which is false and would be the more comfortable lie.
             */
            return Response<TaskTypeGoverningDocumentsDto>.Success(
                new TaskTypeGoverningDocumentsDto([], named.Count, named, []), 200, query.CorrelationId);
        }

        var entries = await _lists.GetEntriesByUidsAsync(current.Id, named, ct);
        var byUid = entries.ToDictionary(e => e.DocumentUid, StringComparer.OrdinalIgnoreCase);

        var unresolved = named.Where(u => !byUid.ContainsKey(u)).ToList();
        var citable = new List<DocumentReferenceEntryDto>();
        var blocked = new List<DocumentReferenceEntryDto>();

        foreach (var uid in named)
        {
            if (!byUid.TryGetValue(uid, out var e)) { continue; }
            var dto = new DocumentReferenceEntryDto(
                e.DocumentUid, e.DocumentCode, e.Title, e.DocumentVersion, e.Status, e.GqmsDomain,
                e.IsMandatoryGroupSop, e.LinkableInErp, e.LinkBlockedReason);
            (e.LinkableInErp ? citable : blocked).Add(dto);
        }

        return Response<TaskTypeGoverningDocumentsDto>.Success(
            new TaskTypeGoverningDocumentsDto(citable, named.Count, unresolved, blocked),
            200, query.CorrelationId);
    }
}
