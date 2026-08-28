using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

/// <summary>
/// "Which period is in force at this instant, at the most specific address I named?" — the HTTP face of
/// <see cref="Read.ICyclePeriodReader"/>. It is a READ: it creates nothing, updates nothing and never invents a period.
/// <para><c>At</c> is required. Defaulting it to "now" server-side would make an audited answer depend on an
/// unrecorded clock reading.</para>
/// <para>Every scope argument is optional, and an omitted one means "do not ask at that level" — which is exactly why
/// an FU06-shaped call (an instant plus a business unit) still answers what FU06 answered, however many country or
/// legal-entity periods the tenant later creates.</para>
/// </summary>
public sealed record ResolveActiveCyclePeriodQuery(
    DateTimeOffset At,
    string? Country,
    Guid? LegalEntityId,
    string? BusinessUnitId) : IRequest<Response<CyclePeriodResolutionDto>>;
