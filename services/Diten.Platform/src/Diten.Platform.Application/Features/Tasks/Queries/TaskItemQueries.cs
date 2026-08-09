using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Queries;

// MOD-0024 — read side. Queries are sealed records; handlers carry no Query suffix.

public sealed record GetTaskItemListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskItemListItemDto>>>;

public sealed record GetTaskItemByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskItemDetailDto>>;

/// <summary>
/// Positions a task may be pooled to (pack §12 K4). Returns the organization unit CODE and NAME alongside the
/// position, because <c>PositionDto</c> exposes only <c>OrganizationUnitId</c> — without the unit label a picker
/// cannot tell "QA Specialist — Facility A" from "QA Specialist — Facility B" and work lands in the wrong pool.
/// Draft/archived positions are excluded (<c>Position.Status</c> defaults to Draft, so an unfiltered list would
/// offer positions that are not real yet).
/// </summary>
public sealed record GetTaskAssignmentPositionLookupQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<AssignablePositionDto>>>;

/// <summary>
/// People a task may be assigned to (pack §12 K6.4). Assignability comes from holding a POSITION: the source is
/// the active <c>PositionAssignment</c> set, which keeps the list consistent with the organization context (K6)
/// and avoids exposing the whole employee directory. A person with no position is therefore absent — the UI must
/// say so plainly rather than render a silent empty list.
///
/// <para>Each row carries the position AND its organization unit, for the same reason the position lookup does:
/// two people holding "QA Specialist" in different facilities are otherwise indistinguishable.</para>
/// </summary>
public sealed record GetTaskAssignmentPersonLookupQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<AssignablePersonDto>>>;

/// <summary>
/// Templates a recurrence rule may be bound to (BL-052).
///
/// <para>A LOOKUP, not a management list: id + name is everything a picker needs. It exists because the rule
/// screen offers "generate each task from this template" and there was no way to enumerate them — the repository
/// could already list them, nothing exposed it, and a picker with no source is a control that can never be
/// filled. Active only: binding a rule to a retired template would generate work from a shape nobody maintains.</para>
/// </summary>
public sealed record GetTaskTemplateLookupQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskTemplateLookupDto>>>;

/// <summary>
/// Every recurrence rule the tenant can see, ACTIVE OR NOT — a paused rule that vanished from the list could
/// never be resumed (Phase 4).
/// </summary>
public sealed record GetTaskRecurrenceRuleListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskRecurrenceRuleDto>>>;

public sealed record GetTaskRecurrenceRuleByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskRecurrenceRuleDto>>;

/// <summary>
/// Every field definition the tenant can see, ACTIVE OR NOT — a retired definition must stay visible so the
/// values already stored under it keep an explanation, and so it can be switched back on.
/// </summary>
public sealed record GetTaskFieldDefinitionListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskFieldDefinitionDto>>>;

public sealed record GetTaskFieldDefinitionByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskFieldDefinitionDto>>;
