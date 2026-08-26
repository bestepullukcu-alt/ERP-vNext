using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkAggregation.Queries;

/// <summary>
/// BL-023 — does the caller HAVE a team at all?
///
/// <para>Asked separately from the work list because the two answers are different questions and the UI needs
/// the first one BEFORE it renders the control: "nobody reports to you" must disable the option and say so,
/// while "your team has no open work" is an empty list under an enabled option. Deriving the first from an empty
/// list would be exactly the silent-empty-state defect this item exists to remove.</para>
/// </summary>
public sealed record GetMyTeamAvailabilityQuery(string CorrelationId)
    : IRequest<Response<TeamAvailabilityDto>>;

/// <summary>
/// Counts only. WHO is in the team is not answered here: membership is already scope-limited (BL-057) and the
/// list endpoint is what serves it — a roster on this endpoint would be a second, unguarded way to enumerate
/// people.
/// </summary>
public sealed record TeamAvailabilityDto(bool HasTeam, int MemberCount);
