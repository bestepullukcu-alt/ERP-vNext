using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Commands;

/// <summary>
/// Confirms a plan (planned → confirmed). This is the ONE gate where the consent guard is fail-closed (D6): a
/// <c>blocked</c>/<c>unknown</c> verdict, or a filter that did not apply, answers 409 and the plan stays <c>planned</c>.
/// Confirm requires the separate <c>crm.planned-visit.confirm</c> permission so the author and the confirmer can differ.
/// </summary>
public sealed record ConfirmPlannedVisitCommand(
    Guid PlannedVisitId,
    int? ExpectedVersion) : IRequest<Response<bool>>;
