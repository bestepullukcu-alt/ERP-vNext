using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Commands;

/// <summary>
/// Cancels a plan (draft/planned/confirmed → cancelled). <see cref="CancellationReason"/> is REQUIRED (V21/AC-CORE-6);
/// the row is never deleted, so a cancelled plan stays readable with its reason and no longer holds a slot (it drops out
/// of the overlap + same-day-type guards).
/// </summary>
public sealed record CancelPlannedVisitCommand(
    Guid PlannedVisitId,
    string? CancellationReason,
    int? ExpectedVersion,
    /// <summary>WP-VW-W2 — a code of the visit-outcome-reason reference set (applies_to ∋ cancel). When absent the legacy
    /// free-text <see cref="CancellationReason"/> is still accepted for one more release (it will become required).</summary>
    string? ReasonCode = null,
    /// <summary>WP-VW-W2 — the note (≤ 500; required when the reason's requires_note).</summary>
    string? Note = null) : IRequest<Response<bool>>;
