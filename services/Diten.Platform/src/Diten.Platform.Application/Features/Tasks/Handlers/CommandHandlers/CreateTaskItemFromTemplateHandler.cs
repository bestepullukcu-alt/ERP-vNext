using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// MOD-0024 Phase 2 — create a task from a reusable template (pack §12 E5), instantiating the template's
/// checklist along with it.
///
/// <para>It translates the template into an ordinary <see cref="CreateTaskItemCommand"/> and delegates. Every
/// rule that governs task creation — assignment resolution, the graded organization-unit fallback, field
/// validation, notification — therefore applies unchanged; re-implementing them here would be a second, subtly
/// different create path.</para>
/// </summary>
public sealed class CreateTaskItemFromTemplateHandler
    : IRequestHandler<CreateTaskItemFromTemplateCommand, Response<Guid>>
{
    private readonly ITaskTemplateRepository _templates;
    private readonly IMediator _mediator;

    public CreateTaskItemFromTemplateHandler(ITaskTemplateRepository templates, IMediator mediator)
    {
        _templates = templates;
        _mediator = mediator;
    }

    public async Task<Response<Guid>> Handle(CreateTaskItemFromTemplateCommand command, CancellationToken ct)
    {
        var request = command.Request;

        // Tenant-scoped repository: another tenant's template does not resolve.
        var template = await _templates.GetByIdAsync(request.TaskTemplateId, ct);
        if (template is null || !template.IsActive)
        {
            return Response<Guid>.Fail(
                "The task template could not be found.",
                404, TaskReasonCodes.TemplateNotFound, command.CorrelationId);
        }

        var target = request.AssignmentTargetOverride ?? template.DefaultAssignmentTarget;

        var createRequest = new CreateTaskItemRequest(
            Title: string.IsNullOrWhiteSpace(request.TitleOverride)
                ? template.TitleTemplate ?? template.Name
                : request.TitleOverride.Trim(),
            Description: template.DescriptionTemplate,
            Priority: template.DefaultPriority,
            AssignmentTarget: target,
            AssigneeUserId: request.AssigneeUserId,
            PoolPositionId: request.PoolPositionId ?? template.DefaultPoolPositionId,
            OrganizationUnitId: null,
            // An explicit due date wins; otherwise the template's offset from today, if it defines one.
            DueAt: request.DueAt
                   ?? (template.DefaultDueInDays is { } days ? DateTimeOffset.UtcNow.AddDays(days) : null),
            StartAt: null,
            PlannedDate: null,
            EstimateHours: null,
            Tags: null,
            ReviewRequired: false,
            ApprovalRequired: false,
            ApprovalManagerUserId: null,
            EmailNotificationsEnabled: true,
            DelegationAllowed: false,
            FieldValues: template.DefaultFieldValues
                .Select(value => new TaskFieldValueDto(value.DefinitionCode, value.ValueType, value.Value))
                .ToList(),
            Watchers: null,
            ParentTaskItemId: null,
            // The template's checklist becomes a live run on the new task — the "instantiates its checklist"
            // half of E5.
            ChecklistTemplateId: template.ChecklistTemplateId);

        /*
         * A template supplies whatever DEFAULT field values it carries and nothing else — there is no form here
         * to ask for the rest. Enforcing requiredness would make every template that predates a required
         * definition unusable, with no screen on which to fix it. The gap is real and recorded (BL-058): the
         * template editor has to offer the configurable fields before this can tighten.
         */
        /*
         * BL-054 — HOW OLD THE TEMPLATE WAS at this moment, carried onto the task.
         *
         * `UpdatedAt ?? CreatedAt`, because those are the same question asked of a template nobody has edited
         * yet. NOT `UtcNow`: the stamp has to name the TEMPLATE's state, not the generation's. A "now" would
         * agree with the creation timestamp the task already carries and answer nothing, while this answers the
         * one question a reader six months later actually has — was the template touched after this task was
         * born, or is what it carries still what the template says?
         */
        return await _mediator.Send(
            new CreateTaskItemCommand(
                createRequest,
                command.CorrelationId,
                EnforceRequiredFields: false,
                TemplateSnapshotAt: template.UpdatedAt ?? template.CreatedAt),
            ct);
    }
}
