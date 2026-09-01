using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// BL-054 — the rules a reusable checklist must satisfy, in ONE place so create and update cannot drift. The
/// same lesson <see cref="TaskRecurrenceRules"/> records a slice earlier: an identical check written out twice
/// is how a third path ends up with none.
/// </summary>
public static class ChecklistTemplateRules
{
    /// <summary>Codes are compared and stored in one canonical spelling, so <c>qa-release</c> and
    /// <c>QA-Release</c> are the same code rather than two templates nobody can tell apart.</summary>
    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static (string ReasonCode, string Message)? Validate(IReadOnlyList<ChecklistTemplateItemDto> items)
    {
        /*
         * AN EMPTY CHECKLIST IS REFUSED.
         *
         * Not pedantry: a template with no steps instantiates an empty list onto every task bound to it, and an
         * empty checklist on screen is indistinguishable from a checklist that failed to load. The author who
         * saved it believes they configured a gate; the holder sees nothing and completes the task. Saying no at
         * the moment of typing costs one message.
         */
        if (items.Count == 0)
        {
            return (TaskReasonCodes.ChecklistTemplateEmpty,
                "A checklist template needs at least one item; an empty one would instantiate nothing.");
        }

        /*
         * ITEM CODES ARE THE JOIN KEY. A run item is matched back to its template item by code, so two items
         * sharing one code make every later tick, edit and removal ambiguous — and the ambiguity surfaces months
         * later on a live task rather than here.
         */
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Code))
            {
                return (TaskReasonCodes.ChecklistItemCodeDuplicate,
                    "Every checklist item needs a code; it is how a ticked step is matched back to the template.");
            }

            if (!seen.Add(item.Code.Trim()))
            {
                return (TaskReasonCodes.ChecklistItemCodeDuplicate,
                    $"Two items share the code '{item.Code.Trim()}'.");
            }
        }

        return null;
    }

    /// <summary>
    /// Turns the wire shape into stored items, renumbering <c>SortOrder</c> from the order they ARRIVED in.
    ///
    /// <para>The renumber is deliberate. The screen orders items by dragging, and a client that sends its own
    /// numbering leaves gaps and ties the moment a row is removed — after which two steps sort by whichever
    /// happens to come out of the driver first, so the same checklist reads in a different order on two screens.
    /// </para>
    /// </summary>
    public static List<ChecklistTemplateItem> ToItems(IReadOnlyList<ChecklistTemplateItemDto> items)
        => items.Select((item, index) => new ChecklistTemplateItem
        {
            Code = item.Code.Trim(),
            /*
             * Exactly one label source survives, and TEXT WINS when both arrive. A tenant author typing into this
             * screen produces text; a resource key they cannot add a line for would render as the key itself,
             * which is the failure ChecklistTemplateItem's own comment records.
             */
            LabelText = string.IsNullOrWhiteSpace(item.LabelText) ? null : item.LabelText.Trim(),
            LabelResourceKey = string.IsNullOrWhiteSpace(item.LabelText)
                ? (string.IsNullOrWhiteSpace(item.LabelResourceKey) ? null : item.LabelResourceKey.Trim())
                : null,
            Requirement = item.Requirement,
            SortOrder = index,
            EvidenceRequired = item.EvidenceRequired
        }).ToList();
}

public sealed class CreateChecklistTemplateHandler
    : IRequestHandler<CreateChecklistTemplateCommand, Response<Guid>>
{
    private readonly IChecklistTemplateRepository _templates;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateChecklistTemplateHandler(
        IChecklistTemplateRepository templates,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _templates = templates;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(CreateChecklistTemplateCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Response<Guid>.Fail(
                "A checklist template needs a code and a name.",
                400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        if (ChecklistTemplateRules.Validate(request.Items) is { } invalid)
        {
            return Response<Guid>.Fail(invalid.Message, 400, invalid.ReasonCode, command.CorrelationId);
        }

        var code = ChecklistTemplateRules.NormalizeCode(request.Code);

        /*
         * Checked against EVERY template, retired ones included — the same rule the task-type catalogue states.
         * A code freed by retirement could be re-used for different steps, and every run instantiated under the
         * old meaning would silently read as belonging to the new one.
         */
        if ((await _templates.ListAllAsync(ct))
            .Any(existing => string.Equals(existing.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<Guid>.Fail(
                $"Another checklist template already uses the code '{code}'.",
                409, TaskReasonCodes.ChecklistTemplateCodeTaken, command.CorrelationId);
        }

        var template = new ChecklistTemplate
        {
            TenantId = _tenantContext.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Items = ChecklistTemplateRules.ToItems(request.Items),
            IsActive = request.IsActive,
            CreatedBy = _currentUser.ActorName
        };

        var created = await _templates.CreateAsync(template, ct);
        return Response<Guid>.Success(created.Id, 201, command.CorrelationId);
    }
}

public sealed class UpdateChecklistTemplateHandler
    : IRequestHandler<UpdateChecklistTemplateCommand, Response<NoContent>>
{
    private readonly IChecklistTemplateRepository _templates;
    private readonly ICurrentUserContext _currentUser;

    public UpdateChecklistTemplateHandler(
        IChecklistTemplateRepository templates, ICurrentUserContext currentUser)
    {
        _templates = templates;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdateChecklistTemplateCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var template = await _templates.GetByIdAsync(command.Id, ct);
        if (template is null || template.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Checklist template not found.",
                404, TaskReasonCodes.ChecklistTemplateNotFound, command.CorrelationId);
        }

        /*
         * ⚠ THE CODE IS REFUSED, NOT IGNORED — the rule the task-type catalogue already writes down. The form
         * sends it read-only, so a request carrying a different one is a client bug or a bypassed form, and
         * quietly keeping the stored value would report success for a change the caller did not get.
         */
        if (!string.Equals(
                ChecklistTemplateRules.NormalizeCode(request.Code ?? string.Empty),
                template.Code, StringComparison.OrdinalIgnoreCase))
        {
            return Response<NoContent>.Fail(
                "A checklist template's code cannot be changed.",
                400, TaskReasonCodes.TemplateCodeImmutable, command.CorrelationId);
        }

        if (ChecklistTemplateRules.Validate(request.Items) is { } invalid)
        {
            return Response<NoContent>.Fail(invalid.Message, 400, invalid.ReasonCode, command.CorrelationId);
        }

        template.Name = request.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        template.Items = ChecklistTemplateRules.ToItems(request.Items);
        template.IsActive = request.IsActive;
        template.UpdatedBy = _currentUser.ActorName;

        if (!await _templates.UpdateAsync(template, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The checklist template changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

public sealed class DeleteChecklistTemplateHandler
    : IRequestHandler<DeleteChecklistTemplateCommand, Response<NoContent>>
{
    private readonly IChecklistTemplateRepository _templates;
    private readonly ICurrentUserContext _currentUser;

    public DeleteChecklistTemplateHandler(
        IChecklistTemplateRepository templates, ICurrentUserContext currentUser)
    {
        _templates = templates;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DeleteChecklistTemplateCommand command, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(command.Id, ct);
        if (template is null || template.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Checklist template not found.",
                404, TaskReasonCodes.ChecklistTemplateNotFound, command.CorrelationId);
        }

        /*
         * SOFT delete, and IsActive goes false with it — both, for the reason the recurrence rule's retire
         * states: two independent readers ask two different questions, and a retired row that answered only one
         * of them would keep being offered by whichever reader forgot. The row survives because task templates
         * and live checklist runs point at it.
         */
        template.DeletedAt = DateTimeOffset.UtcNow;
        template.IsActive = false;
        template.UpdatedBy = _currentUser.ActorName;

        if (!await _templates.UpdateAsync(template, template.Version, ct))
        {
            return Response<NoContent>.Fail(
                "The checklist template changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
