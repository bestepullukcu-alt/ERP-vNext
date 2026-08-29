using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Commands;

/// <summary>
/// Ends a period. Works from <c>draft</c> (a plan that never ran, closed with a trace instead of deleted) and from
/// <c>active</c>. <c>closed</c> is terminal: there is no reopen command anywhere, because plans, visits and reports
/// already point at this period by id.
/// </summary>
public sealed record CloseCyclePeriodCommand(
    Guid CyclePeriodId, int? ExpectedVersion) : IRequest<Response<bool>>;
