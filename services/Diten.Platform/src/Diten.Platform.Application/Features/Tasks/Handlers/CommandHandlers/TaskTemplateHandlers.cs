using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// BL-054 — the rules a reusable task shape must satisfy, in ONE place so create and update cannot drift.
/// </summary>
public static class TaskTemplateRules
{
    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    /// <summary>
    /// THE DEFAULT ASSIGNMENT A TEMPLATE IS ALLOWED TO CARRY.
    ///
    /// <para><b><c>Person</c> is refused, and that is not an omission.</b> A template has a
    /// <c>DefaultPoolPositionId</c> and no assignee field at all — there is nowhere to put the person. A template
    /// saying "assign to a person" therefore names nobody, and the generated task falls straight into the failure
    /// the recurrence rule already paid for once: work created for <c>Guid.Empty</c>, in nobody's list, with its
    /// period consumed so it can never be regenerated. A rule that names a person still works — it passes its own
    /// assignment as an override, which is exactly the precedence the form states.</para>
    ///
    /// <para><c>SelfAssigned</c> IS legal here, unlike on a recurrence rule, and the difference is real rather
    /// than an inconsistency: a template is also used by a person pressing "create from template", and for them
    /// "mine" is a meaningful default. It is the SWEEP that has no self, and a sweep-driven create always arrives
    /// with the rule's own assignment overriding this.</para>
    /// </summary>
    public static (string ReasonCode, string Message)? ValidateAssignment(
        TaskAssignmentTarget target, Guid? poolPositionId)
    {
        if (target == TaskAssignmentTarget.Person)
        {
            return (TaskReasonCodes.TemplateAssignmentInvalid,
                "A template cannot default to a named person: it carries no assignee field, so the work would " +
                "reach nobody. Name the person on the recurrence rule instead.");
        }

        if (target == TaskAssignmentTarget.PositionPool && (poolPositionId is null || poolPositionId == Guid.Empty))
        {
            return (TaskReasonCodes.TemplateAssignmentInvalid,
                "A pool default needs the position the work is offered to.");
        }

        // A pool id left behind on a non-pool default is an identity nothing reads today and something reads
        // tomorrow — refused rather than silently dropped, so the saved record says what the author chose.
        if (target != TaskAssignmentTarget.PositionPool && poolPositionId is { } stale && stale != Guid.Empty)
        {
            return (TaskReasonCodes.TemplateAssignmentInvalid,
                "A position was supplied for a default that is not a pool.");
        }

        return null;
    }

    /// <summary>A negative or zero due offset would make every generated task due before it existed.</summary>
    public static (string ReasonCode, string Message)? ValidateDueInDays(int? dueInDays)
        => dueInDays is { } days && days < 1
            ? (TaskReasonCodes.ValidationFailed, "The due offset must be at least one day.")
            : null;
}

public sealed class CreateTaskTemplateHandler : IRequestHandler<CreateTaskTemplateCommand, Response<Guid>>
{
    private readonly ITaskTemplateRepository _templates;
    private readonly IChecklistTemplateRepository _checklists;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateTaskTemplateHandler(
        ITaskTemplateRepository templates,
        IChecklistTemplateRepository checklists,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _templates = templates;
        _checklists = checklists;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(CreateTaskTemplateCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Response<Guid>.Fail(
                "A task template needs a code and a name.",
                400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        if (TaskTemplateRules.ValidateAssignment(request.DefaultAssignmentTarget, request.DefaultPoolPositionId)
            is { } assignmentInvalid)
        {
            return Response<Guid>.Fail(
                assignmentInvalid.Message, 400, assignmentInvalid.ReasonCode, command.CorrelationId);
        }

        if (TaskTemplateRules.ValidateDueInDays(request.DefaultDueInDays) is { } dueInvalid)
        {
            return Response<Guid>.Fail(dueInvalid.Message, 400, dueInvalid.ReasonCode, command.CorrelationId);
        }

        if (await ResolveChecklistAsync(_checklists, request.ChecklistTemplateId, ct) is { } checklistInvalid)
        {
            return Response<Guid>.Fail(
                checklistInvalid.Message, 400, checklistInvalid.ReasonCode, command.CorrelationId);
        }

        var code = TaskTemplateRules.NormalizeCode(request.Code);
        if ((await _templates.ListAllAsync(ct))
            .Any(existing => string.Equals(existing.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<Guid>.Fail(
                $"Another task template already uses the code '{code}'.",
                409, TaskReasonCodes.TaskTemplateCodeTaken, command.CorrelationId);
        }

        var template = new TaskTemplate
        {
            TenantId = _tenantContext.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            TitleTemplate = Trimmed(request.TitleTemplate),
            DescriptionTemplate = Trimmed(request.DescriptionTemplate),
            DefaultPriority = request.DefaultPriority,
            DefaultAssignmentTarget = request.DefaultAssignmentTarget,
            DefaultPoolPositionId = Normalized(request.DefaultPoolPositionId),
            DefaultDueInDays = request.DefaultDueInDays,
            ChecklistTemplateId = Normalized(request.ChecklistTemplateId),
            // Guid.Empty from a form's blank option means "every company", never "company zero".
            LegalEntityId = Normalized(request.LegalEntityId),
            IsActive = request.IsActive,
            CreatedBy = _currentUser.ActorName
        };

        var created = await _templates.CreateAsync(template, ct);
        return Response<Guid>.Success(created.Id, 201, command.CorrelationId);
    }

    internal static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static Guid? Normalized(Guid? value)
        => value is { } id && id != Guid.Empty ? id : null;

    /// <summary>
    /// The bound checklist must EXIST and still be offerable.
    ///
    /// <para>Checked at the write rather than at generation, and this is the same class of defect as the
    /// recurrence rule that generated work assigned to nobody: a template bound to a checklist that cannot
    /// resolve produces tasks with no gates at all, silently. It looks configured and does the opposite of what
    /// it says, and nobody finds out until the gate the checklist existed to enforce has already been passed.
    /// </para>
    /// </summary>
    internal static async Task<(string ReasonCode, string Message)?> ResolveChecklistAsync(
        IChecklistTemplateRepository checklists, Guid? checklistTemplateId, CancellationToken ct)
    {
        if (Normalized(checklistTemplateId) is not { } id)
        {
            // No checklist is a legitimate template: a reminder with a title and a due date is real work.
            return null;
        }

        // Tenant-scoped repository: another tenant's checklist does not resolve, so the caller learns nothing
        // about its existence either.
        var checklist = await checklists.GetByIdAsync(id, ct);
        return checklist is null || checklist.DeletedAt is not null || !checklist.IsActive
            ? (TaskReasonCodes.TemplateChecklistUnresolved,
                "The checklist template named here does not exist or has been retired.")
            : null;
    }
}

public sealed class UpdateTaskTemplateHandler : IRequestHandler<UpdateTaskTemplateCommand, Response<NoContent>>
{
    private readonly ITaskTemplateRepository _templates;
    private readonly IChecklistTemplateRepository _checklists;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTaskTemplateHandler(
        ITaskTemplateRepository templates,
        IChecklistTemplateRepository checklists,
        ICurrentUserContext currentUser)
    {
        _templates = templates;
        _checklists = checklists;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdateTaskTemplateCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var template = await _templates.GetByIdAsync(command.Id, ct);
        if (template is null || template.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Task template not found.", 404, TaskReasonCodes.TemplateNotFound, command.CorrelationId);
        }

        if (!string.Equals(
                TaskTemplateRules.NormalizeCode(request.Code ?? string.Empty),
                template.Code, StringComparison.OrdinalIgnoreCase))
        {
            return Response<NoContent>.Fail(
                "A task template's code cannot be changed.",
                400, TaskReasonCodes.TemplateCodeImmutable, command.CorrelationId);
        }

        if (TaskTemplateRules.ValidateAssignment(request.DefaultAssignmentTarget, request.DefaultPoolPositionId)
            is { } assignmentInvalid)
        {
            return Response<NoContent>.Fail(
                assignmentInvalid.Message, 400, assignmentInvalid.ReasonCode, command.CorrelationId);
        }

        if (TaskTemplateRules.ValidateDueInDays(request.DefaultDueInDays) is { } dueInvalid)
        {
            return Response<NoContent>.Fail(dueInvalid.Message, 400, dueInvalid.ReasonCode, command.CorrelationId);
        }

        if (await CreateTaskTemplateHandler.ResolveChecklistAsync(_checklists, request.ChecklistTemplateId, ct)
            is { } checklistInvalid)
        {
            return Response<NoContent>.Fail(
                checklistInvalid.Message, 400, checklistInvalid.ReasonCode, command.CorrelationId);
        }

        template.Name = request.Name.Trim();
        template.TitleTemplate = CreateTaskTemplateHandler.Trimmed(request.TitleTemplate);
        template.DescriptionTemplate = CreateTaskTemplateHandler.Trimmed(request.DescriptionTemplate);
        template.DefaultPriority = request.DefaultPriority;
        template.DefaultAssignmentTarget = request.DefaultAssignmentTarget;
        template.DefaultPoolPositionId = CreateTaskTemplateHandler.Normalized(request.DefaultPoolPositionId);
        template.DefaultDueInDays = request.DefaultDueInDays;
        template.ChecklistTemplateId = CreateTaskTemplateHandler.Normalized(request.ChecklistTemplateId);
        template.LegalEntityId = CreateTaskTemplateHandler.Normalized(request.LegalEntityId);
        template.IsActive = request.IsActive;
        template.UpdatedBy = _currentUser.ActorName;

        /*
         * DefaultFieldValues is deliberately NOT touched by this edit, and the omission is the interesting part:
         * this request does not carry them, so writing the property would blank whatever a template already
         * holds. Offering them in the form is BL-058 — until then, a full-replace update that quietly replaced
         * them with nothing would be the FULL-REPLACE trap this module has already been bitten by twice.
         */

        if (!await _templates.UpdateAsync(template, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task template changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

public sealed class DeleteTaskTemplateHandler : IRequestHandler<DeleteTaskTemplateCommand, Response<NoContent>>
{
    private readonly ITaskTemplateRepository _templates;
    private readonly ICurrentUserContext _currentUser;

    public DeleteTaskTemplateHandler(ITaskTemplateRepository templates, ICurrentUserContext currentUser)
    {
        _templates = templates;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DeleteTaskTemplateCommand command, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(command.Id, ct);
        if (template is null || template.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Task template not found.", 404, TaskReasonCodes.TemplateNotFound, command.CorrelationId);
        }

        template.DeletedAt = DateTimeOffset.UtcNow;
        template.IsActive = false;
        template.UpdatedBy = _currentUser.ActorName;

        if (!await _templates.UpdateAsync(template, template.Version, ct))
        {
            return Response<NoContent>.Fail(
                "The task template changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
