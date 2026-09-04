using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

// BL-054 — the read side of the template chain. Shaped exactly like the recurrence-rule query handlers beside
// them, including the one rule that matters on a management surface: a RETIRED row leaves the list, a PAUSED one
// stays. A template that vanished when it was switched off could never be switched back on.

public sealed class GetChecklistTemplateListHandler
    : IRequestHandler<GetChecklistTemplateListQuery, Response<IReadOnlyList<ChecklistTemplateDto>>>
{
    private readonly IChecklistTemplateRepository _templates;

    public GetChecklistTemplateListHandler(IChecklistTemplateRepository templates) => _templates = templates;

    public async Task<Response<IReadOnlyList<ChecklistTemplateDto>>> Handle(
        GetChecklistTemplateListQuery request, CancellationToken ct)
    {
        IReadOnlyList<ChecklistTemplateDto> result = (await _templates.ListAllAsync(ct))
            .Where(template => template.DeletedAt is null)
            .Select(ChecklistTemplateMapper.ToDto)
            .ToList();

        return Response<IReadOnlyList<ChecklistTemplateDto>>.Success(result, correlationId: request.CorrelationId);
    }
}

public sealed class GetChecklistTemplateByIdHandler
    : IRequestHandler<GetChecklistTemplateByIdQuery, Response<ChecklistTemplateDto>>
{
    private readonly IChecklistTemplateRepository _templates;

    public GetChecklistTemplateByIdHandler(IChecklistTemplateRepository templates) => _templates = templates;

    public async Task<Response<ChecklistTemplateDto>> Handle(
        GetChecklistTemplateByIdQuery request, CancellationToken ct)
    {
        // Tenant-scoped repository: another tenant's template does not resolve, so the caller learns nothing
        // about its existence.
        var template = await _templates.GetByIdAsync(request.Id, ct);
        if (template is null || template.DeletedAt is not null)
        {
            return Response<ChecklistTemplateDto>.Fail(
                "Checklist template not found.",
                404, TaskReasonCodes.ChecklistTemplateNotFound, request.CorrelationId);
        }

        return Response<ChecklistTemplateDto>.Success(
            ChecklistTemplateMapper.ToDto(template), correlationId: request.CorrelationId);
    }
}

/// <summary>
/// What the TASK-TEMPLATE form's checklist picker is filled from — active only.
///
/// <para>This is the endpoint that stops the defect repeating one level in. The recurrence rule's template picker
/// existed for a long time with nothing behind it; a checklist picker shipped the same way would be the identical
/// empty control, and the person filling the task-template form would have no way to tell a missing endpoint from
/// an empty tenant.</para>
/// </summary>
public sealed class GetChecklistTemplateLookupHandler
    : IRequestHandler<GetChecklistTemplateLookupQuery, Response<IReadOnlyList<ChecklistTemplateLookupDto>>>
{
    private readonly IChecklistTemplateRepository _templates;

    public GetChecklistTemplateLookupHandler(IChecklistTemplateRepository templates) => _templates = templates;

    public async Task<Response<IReadOnlyList<ChecklistTemplateLookupDto>>> Handle(
        GetChecklistTemplateLookupQuery request, CancellationToken ct)
    {
        // Binding a template to a RETIRED checklist would go on instantiating steps nobody maintains any more —
        // the same reason the task-template lookup offers active rows only.
        IReadOnlyList<ChecklistTemplateLookupDto> result = (await _templates.ListActiveAsync(ct))
            .Where(template => template.DeletedAt is null)
            .Select(template => new ChecklistTemplateLookupDto(
                template.Id, template.Code, template.Name, template.Items.Count))
            .ToList();

        return Response<IReadOnlyList<ChecklistTemplateLookupDto>>.Success(
            result, correlationId: request.CorrelationId);
    }
}

public sealed class GetTaskTemplateListHandler
    : IRequestHandler<GetTaskTemplateListQuery, Response<IReadOnlyList<TaskTemplateDto>>>
{
    private readonly ITaskTemplateRepository _templates;

    public GetTaskTemplateListHandler(ITaskTemplateRepository templates) => _templates = templates;

    public async Task<Response<IReadOnlyList<TaskTemplateDto>>> Handle(
        GetTaskTemplateListQuery request, CancellationToken ct)
    {
        IReadOnlyList<TaskTemplateDto> result = (await _templates.ListAllAsync(ct))
            .Where(template => template.DeletedAt is null)
            .Select(TaskTemplateMapper.ToDto)
            .ToList();

        return Response<IReadOnlyList<TaskTemplateDto>>.Success(result, correlationId: request.CorrelationId);
    }
}

public sealed class GetTaskTemplateByIdHandler
    : IRequestHandler<GetTaskTemplateByIdQuery, Response<TaskTemplateDto>>
{
    private readonly ITaskTemplateRepository _templates;

    public GetTaskTemplateByIdHandler(ITaskTemplateRepository templates) => _templates = templates;

    public async Task<Response<TaskTemplateDto>> Handle(
        GetTaskTemplateByIdQuery request, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(request.Id, ct);
        if (template is null || template.DeletedAt is not null)
        {
            return Response<TaskTemplateDto>.Fail(
                "Task template not found.", 404, TaskReasonCodes.TemplateNotFound, request.CorrelationId);
        }

        return Response<TaskTemplateDto>.Success(
            TaskTemplateMapper.ToDto(template), correlationId: request.CorrelationId);
    }
}

public static class ChecklistTemplateMapper
{
    public static ChecklistTemplateDto ToDto(ChecklistTemplate template) => new(
        template.Id,
        template.Code,
        template.Name,
        template.Description,
        template.Items
            // Ordered HERE rather than trusted from storage: the list is a document array, and a reader that
            // showed the steps in insertion order would show a reordered checklist in its old order.
            .OrderBy(item => item.SortOrder)
            .Select(item => new ChecklistTemplateItemDto(
                item.Code,
                item.LabelResourceKey,
                item.LabelText,
                item.Requirement,
                item.SortOrder,
                item.EvidenceRequired))
            .ToList(),
        template.Items.Count,
        template.IsActive,
        template.Version,
        template.CreatedAt,
        template.UpdatedAt);
}

public static class TaskTemplateMapper
{
    public static TaskTemplateDto ToDto(TaskTemplate template) => new(
        template.Id,
        template.Code,
        template.Name,
        template.TitleTemplate,
        template.DescriptionTemplate,
        // Enums as STRINGS on the wire — the live Platform convention, and one an enum-as-number defect already
        // cost this module once.
        template.DefaultPriority.ToString(),
        template.DefaultAssignmentTarget.ToString(),
        template.DefaultPoolPositionId,
        template.DefaultDueInDays,
        template.ChecklistTemplateId,
        template.LegalEntityId,
        template.IsActive,
        template.Version,
        template.CreatedAt,
        template.UpdatedAt);
}
