using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 D9 — ghost values from the caller's plan blocks for (day, task) cells with no timer time, on
/// days up to local today. READ ONLY: nothing is written, ever; accepting a row is a save with <c>source: "Plan"</c>.</summary>
public sealed record GetPlanFillInQuery(string WeekKey, string CorrelationId) : IRequest<Response<PlanFillInDto>>;
