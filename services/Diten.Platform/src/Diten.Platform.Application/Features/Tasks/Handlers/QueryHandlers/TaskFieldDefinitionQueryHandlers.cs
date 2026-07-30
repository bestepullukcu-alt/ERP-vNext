using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>Phase 5 — reading the configurable field catalogue.</summary>
public sealed class GetTaskFieldDefinitionListHandler
    : IRequestHandler<GetTaskFieldDefinitionListQuery, Response<IReadOnlyList<TaskFieldDefinitionDto>>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;

    public GetTaskFieldDefinitionListHandler(ITaskFieldDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Response<IReadOnlyList<TaskFieldDefinitionDto>>> Handle(
        GetTaskFieldDefinitionListQuery request, CancellationToken ct)
    {
        // Retired definitions are not offered for management, but PAUSED ones are: a definition that vanished
        // when it was switched off could never be switched back on.
        IReadOnlyList<TaskFieldDefinitionDto> result = (await _definitions.ListAllAsync(ct))
            .Where(definition => definition.DeletedAt is null)
            .Select(TaskFieldDefinitionMapper.ToDto)
            .ToList();

        return Response<IReadOnlyList<TaskFieldDefinitionDto>>.Success(result, correlationId: request.CorrelationId);
    }
}

public sealed class GetTaskFieldDefinitionByIdHandler
    : IRequestHandler<GetTaskFieldDefinitionByIdQuery, Response<TaskFieldDefinitionDto>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;

    public GetTaskFieldDefinitionByIdHandler(ITaskFieldDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Response<TaskFieldDefinitionDto>> Handle(
        GetTaskFieldDefinitionByIdQuery request, CancellationToken ct)
    {
        // Tenant-scoped repository: another tenant's definition does not resolve, so the caller learns nothing
        // about its existence.
        var definition = await _definitions.GetByIdAsync(request.Id, ct);
        if (definition is null || definition.DeletedAt is not null)
        {
            return Response<TaskFieldDefinitionDto>.Fail(
                "Field definition not found.", 404,
                TaskReasonCodes.FieldDefinitionNotFound, request.CorrelationId);
        }

        return Response<TaskFieldDefinitionDto>.Success(
            TaskFieldDefinitionMapper.ToDto(definition), correlationId: request.CorrelationId);
    }
}

public static class TaskFieldDefinitionMapper
{
    public static TaskFieldDefinitionDto ToDto(TaskFieldDefinition definition) => new(
        definition.Id,
        definition.Code,
        // BOTH label sources cross the wire, and exactly one is populated. The client decides which contract
        // label form to render from which one is present — it never guesses, and it never falls back to the code.
        definition.LabelResourceKey,
        definition.LabelText,
        // Enums as STRINGS, the live Platform convention — an enum reaching a client as a number is a defect this
        // module has already shipped twice.
        definition.ValueType.ToString(),
        definition.Section,
        definition.Importance.ToString(),
        definition.IsRequired,
        definition.SortOrder,
        definition.OptionsSourceKind.ToString(),
        definition.OptionsSourceKey,
        definition.AppliesToModuleCode,
        definition.Classification.ToString(),
        definition.DefaultAccessState.ToString(),
        definition.IsActive,
        definition.Version,
        definition.CreatedAt);
}
