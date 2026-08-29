using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

/// <summary>The period grid. <c>CoversDate</c> answers "which period(s) contain this day?" without resolving anything:
/// it is a filter over rows, not the <c>resolve-active</c> decision — it ignores status and precedence on purpose.
/// <para>FU07 scope filters NARROW the listing; they never apply precedence. Asking for country=TR shows the TR
/// periods, not "the period a TR caller would resolve to".</para></summary>
public sealed record GetCyclePeriodListQuery(
    string? CycleStatus,
    int? Year,
    string? ScopeType,
    string? Country,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    string? CycleCode,
    DateTimeOffset? CoversDate,
    string? Search) : IRequest<Response<CyclePeriodListDto>>;
