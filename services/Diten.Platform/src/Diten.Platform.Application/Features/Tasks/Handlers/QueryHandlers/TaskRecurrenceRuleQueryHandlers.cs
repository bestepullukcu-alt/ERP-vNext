using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>Phase 4 — reading recurrence rules. No screen yet (deliberately out of scope); the API is the surface.</summary>
public sealed class GetTaskRecurrenceRuleListHandler
    : IRequestHandler<GetTaskRecurrenceRuleListQuery, Response<IReadOnlyList<TaskRecurrenceRuleDto>>>
{
    private readonly ITaskRecurrenceRuleRepository _rules;

    public GetTaskRecurrenceRuleListHandler(ITaskRecurrenceRuleRepository rules) => _rules = rules;

    public async Task<Response<IReadOnlyList<TaskRecurrenceRuleDto>>> Handle(
        GetTaskRecurrenceRuleListQuery request, CancellationToken ct)
    {
        // Soft-deleted rules stay in the collection because generated tasks point at them; they are not offered
        // here, because a retired rule is not something anyone manages any more.
        IReadOnlyList<TaskRecurrenceRuleDto> result = (await _rules.ListAllAsync(ct))
            .Where(rule => rule.DeletedAt is null)
            .Select(TaskRecurrenceRuleMapper.ToDto)
            .ToList();

        return Response<IReadOnlyList<TaskRecurrenceRuleDto>>.Success(result, correlationId: request.CorrelationId);
    }
}

public sealed class GetTaskRecurrenceRuleByIdHandler
    : IRequestHandler<GetTaskRecurrenceRuleByIdQuery, Response<TaskRecurrenceRuleDto>>
{
    private readonly ITaskRecurrenceRuleRepository _rules;

    public GetTaskRecurrenceRuleByIdHandler(ITaskRecurrenceRuleRepository rules) => _rules = rules;

    public async Task<Response<TaskRecurrenceRuleDto>> Handle(
        GetTaskRecurrenceRuleByIdQuery request, CancellationToken ct)
    {
        // The tenant-scoped repository makes this a cross-tenant check too: another tenant's rule simply does
        // not resolve, so the caller learns nothing about its existence.
        var rule = await _rules.GetByIdAsync(request.Id, ct);
        if (rule is null || rule.DeletedAt is not null)
        {
            return Response<TaskRecurrenceRuleDto>.Fail(
                "Recurrence rule not found.", 404, TaskReasonCodes.RecurrenceRuleNotFound, request.CorrelationId);
        }

        return Response<TaskRecurrenceRuleDto>.Success(
            TaskRecurrenceRuleMapper.ToDto(rule), correlationId: request.CorrelationId);
    }
}

public static class TaskRecurrenceRuleMapper
{
    public static TaskRecurrenceRuleDto ToDto(TaskRecurrenceRule rule) => new(
        rule.Id,
        rule.Name,
        // Enum as a STRING on the wire — the live Platform convention, and the one an enum-as-number defect
        // already cost this module once.
        rule.Frequency.ToString(),
        rule.Interval,
        rule.StartsAt,
        rule.EndsAt,
        rule.TaskTemplateId,
        rule.IsActive,
        rule.LastProcessInstanceId,
        rule.LastGeneratedAt,
        rule.Version,
        rule.CreatedAt);
}
