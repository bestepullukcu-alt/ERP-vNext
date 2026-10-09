using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitWorkspace.Queries;

/// <summary>WP-VW-W2 (A1) — the active reasons of the visit-outcome-reason set for one action (cancel / missed /
/// reschedule; null = every active reason), labelled in <paramref name="Language"/> (two-letter, en default).</summary>
public sealed record GetVisitReasonsQuery(string? AppliesTo, string? Language) : IRequest<Response<VisitReasonListDto>>;

/// <summary>WP-VW-W2 (A2) — the next 8 days a visit can be moved to, with the rep's load.</summary>
public sealed record GetRescheduleOptionsQuery(Guid PlannedVisitId) : IRequest<Response<RescheduleOptionsDto>>;

/// <summary>WP-VW-W2 (A4) — the unified workspace calendar of [From, To] (≤ 42 days) for the caller (or, with read-all,
/// for <paramref name="ResourceId"/>).</summary>
public sealed record GetWorkspaceCalendarQuery(string? From, string? To, string? ResourceId = null)
    : IRequest<Response<WorkspaceCalendarDto>>;

/// <summary>WP-VW-W2 — what the workspace publishes (statuses incl. draft, reason set, limits, codes).</summary>
public sealed record GetVisitWorkspaceContractQuery : IRequest<Response<VisitWorkspaceContractDto>>;
