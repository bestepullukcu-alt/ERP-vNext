using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Commands;

/// <summary>
/// Puts a period live. This is the single gate where the active-overlap ban is enforced, fail-closed: if another active
/// period of the same business-unit scope shares even one day, the answer is 409 and the row stays <c>draft</c>.
/// </summary>
public sealed record ActivateCyclePeriodCommand(
    Guid CyclePeriodId, int? ExpectedVersion) : IRequest<Response<bool>>;
