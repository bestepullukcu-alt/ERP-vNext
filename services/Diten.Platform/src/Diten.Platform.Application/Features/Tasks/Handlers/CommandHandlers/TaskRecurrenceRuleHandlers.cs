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
/// Phase 4 — the rules a recurrence definition must satisfy, in ONE place so the create and update paths cannot
/// drift. The reviewer requirement taught this lesson a slice ago: approval's identical check written out three
/// times is how a fourth path ends up with none.
/// </summary>
public static class TaskRecurrenceRules
{
    /// <summary>
    /// A rule with no frequency is a schedule that never fires — accepting it would put a row in the list that
    /// looks live and produces nothing, which reads as a broken sweep rather than a misconfigured rule.
    /// </summary>
    public static string? Validate(TaskRecurrenceFrequency frequency, DateTimeOffset? startsAt, DateTimeOffset? endsAt)
    {
        if (frequency == TaskRecurrenceFrequency.None)
        {
            return TaskReasonCodes.RecurrenceFrequencyRequired;
        }

        // Ends before it starts: no occurrence can ever fall inside the window.
        if (startsAt is { } start && endsAt is { } end && end < start)
        {
            return TaskReasonCodes.RecurrenceWindowInvalid;
        }

        return null;
    }
}

public sealed class CreateTaskRecurrenceRuleHandler
    : IRequestHandler<CreateTaskRecurrenceRuleCommand, Response<Guid>>
{
    private readonly ITaskRecurrenceRuleRepository _rules;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateTaskRecurrenceRuleHandler(
        ITaskRecurrenceRuleRepository rules,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _rules = rules;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(CreateTaskRecurrenceRuleCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (TaskRecurrenceRules.Validate(request.Frequency, request.StartsAt, request.EndsAt) is { } reasonCode)
        {
            return Response<Guid>.Fail(
                "The recurrence rule cannot produce any occurrence.", 400, reasonCode, command.CorrelationId);
        }

        var rule = new TaskRecurrenceRule
        {
            TenantId = _tenantContext.TenantId,
            Name = request.Name.Trim(),
            Frequency = request.Frequency,
            // An interval below 1 would make every occurrence the same instant; 1 is "every period".
            Interval = Math.Max(1, request.Interval),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            TaskTemplateId = request.TaskTemplateId,
            IsActive = request.IsActive,
            CreatedBy = _currentUser.ActorName
        };

        var created = await _rules.CreateAsync(rule, ct);
        return Response<Guid>.Success(created.Id, 201, command.CorrelationId);
    }
}

public sealed class UpdateTaskRecurrenceRuleHandler
    : IRequestHandler<UpdateTaskRecurrenceRuleCommand, Response<NoContent>>
{
    private readonly ITaskRecurrenceRuleRepository _rules;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTaskRecurrenceRuleHandler(ITaskRecurrenceRuleRepository rules, ICurrentUserContext currentUser)
    {
        _rules = rules;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdateTaskRecurrenceRuleCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var rule = await _rules.GetByIdAsync(command.Id, ct);
        if (rule is null || rule.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Recurrence rule not found.", 404, TaskReasonCodes.RecurrenceRuleNotFound, command.CorrelationId);
        }

        if (TaskRecurrenceRules.Validate(request.Frequency, request.StartsAt, request.EndsAt) is { } reasonCode)
        {
            return Response<NoContent>.Fail(
                "The recurrence rule cannot produce any occurrence.", 400, reasonCode, command.CorrelationId);
        }

        rule.Name = request.Name.Trim();
        rule.Frequency = request.Frequency;
        rule.Interval = Math.Max(1, request.Interval);
        rule.StartsAt = request.StartsAt;
        rule.EndsAt = request.EndsAt;
        rule.TaskTemplateId = request.TaskTemplateId;
        rule.IsActive = request.IsActive;
        rule.UpdatedBy = _currentUser.ActorName;

        /*
         * LastProcessInstanceId is deliberately NOT cleared by an edit. It names the last period actually
         * produced, and clearing it would let a re-pointed rule regenerate a period it has already made — the
         * duplicate this whole slice exists to prevent, arriving through the edit form instead of the sweep.
         */

        if (!await _rules.UpdateAsync(rule, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The rule changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

public sealed class DeleteTaskRecurrenceRuleHandler
    : IRequestHandler<DeleteTaskRecurrenceRuleCommand, Response<NoContent>>
{
    private readonly ITaskRecurrenceRuleRepository _rules;
    private readonly ICurrentUserContext _currentUser;

    public DeleteTaskRecurrenceRuleHandler(ITaskRecurrenceRuleRepository rules, ICurrentUserContext currentUser)
    {
        _rules = rules;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DeleteTaskRecurrenceRuleCommand command, CancellationToken ct)
    {
        var rule = await _rules.GetByIdAsync(command.Id, ct);
        if (rule is null || rule.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Recurrence rule not found.", 404, TaskReasonCodes.RecurrenceRuleNotFound, command.CorrelationId);
        }

        /*
         * SOFT delete, and IsActive goes false with it.
         *
         * Both, not either: the sweep checks three independent reasons a rule owes nothing, and a retired rule
         * that only stamped DeletedAt would keep producing work if any future reader forgot one of them. The row
         * itself survives because generated tasks point at it, and a hard delete would orphan their explanation.
         */
        rule.DeletedAt = DateTimeOffset.UtcNow;
        rule.IsActive = false;
        rule.UpdatedBy = _currentUser.ActorName;

        if (!await _rules.UpdateAsync(rule, rule.Version, ct))
        {
            return Response<NoContent>.Fail(
                "The rule changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
