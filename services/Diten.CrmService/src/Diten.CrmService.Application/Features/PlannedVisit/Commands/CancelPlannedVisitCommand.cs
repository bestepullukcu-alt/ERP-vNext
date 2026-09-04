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
    int? ExpectedVersion) : IRequest<Response<bool>>;
