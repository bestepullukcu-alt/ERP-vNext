using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Commands;

// BL-054 — the write side of the template chain. Two aggregates, five verbs each, shaped exactly like the
// recurrence-rule commands beside them: a second pattern for a sibling screen would make one module read as two
// products.

/// <summary>
/// Define a reusable checklist. Comes FIRST in this slice on purpose — the task template's form has a checklist
/// picker, and shipping that picker before its source would repeat, one level in, the defect this whole slice
/// exists to close: a live-looking control that can never be filled.
/// </summary>
public sealed record CreateChecklistTemplateCommand(CreateChecklistTemplateRequest Request, string CorrelationId)
    : IRequest<Response<Guid>>;

/// <summary>
/// Edit a checklist template. Expected-version write, like every other MOD-0024 edit — two people rewriting one
/// gate at once must not silently merge into a third gate neither of them chose.
/// </summary>
public sealed record UpdateChecklistTemplateCommand(
    Guid Id, UpdateChecklistTemplateRequest Request, string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>
/// Retire a checklist template. SOFT: <c>DeletedAt</c> is stamped and the row stays, because task templates and
/// live checklist runs point at it and a hard delete would orphan their explanation.
/// </summary>
public sealed record DeleteChecklistTemplateCommand(Guid Id, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record CreateTaskTemplateCommand(CreateTaskTemplateRequest Request, string CorrelationId)
    : IRequest<Response<Guid>>;

public sealed record UpdateTaskTemplateCommand(
    Guid Id, UpdateTaskTemplateRequest Request, string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>
/// Retire a task template. SOFT, and for a sharper reason than usual: recurrence rules hold
/// <c>TaskTemplateId</c>, and generated tasks are explained by the template they came from. A hard delete would
/// leave live schedules pointing at nothing.
/// </summary>
public sealed record DeleteTaskTemplateCommand(Guid Id, string CorrelationId) : IRequest<Response<NoContent>>;
