using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Commands;

/// <summary>Define a recurrence rule (Phase 4). The rule is a DEFINITION; the sweep is what acts on it.</summary>
public sealed record CreateTaskRecurrenceRuleCommand(CreateTaskRecurrenceRuleRequest Request, string CorrelationId)
    : IRequest<Response<Guid>>;

/// <summary>
/// Edit a rule. Expected-version write, like every other MOD-0024 edit — two people re-pointing one schedule at
/// once must not silently merge into a third schedule neither of them chose.
/// </summary>
public sealed record UpdateTaskRecurrenceRuleCommand(Guid Id, UpdateTaskRecurrenceRuleRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>
/// Retire a rule. SOFT: <c>DeletedAt</c> is stamped and the row stays, because tasks already generated point at
/// it through <c>RecurrenceRuleId</c> and a hard delete would orphan their explanation.
/// </summary>
public sealed record DeleteTaskRecurrenceRuleCommand(Guid Id, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>
/// Generate whatever the tenant's rules owe RIGHT NOW. Tenant-scoped and callable on its own, exactly like
/// MOD-0023's <c>RunWorkflowEscalationsCommand</c>: the sweep runs it inside each tenant's scope, and it stays
/// reachable for a test or an explicit trigger without needing the scheduler at all.
/// </summary>
public sealed record GenerateDueRecurringTasksCommand(
    DateTimeOffset? NowUtc,
    int MaxRules,
    string CorrelationId) : IRequest<Response<GenerateDueRecurringTasksResponse>>;
