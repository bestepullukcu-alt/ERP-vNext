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
