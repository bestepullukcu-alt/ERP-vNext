using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Commands;

/// <summary>
/// Creates a planning period. It is always born <c>draft</c>: putting a period live is a separate endpoint with a
/// separate permission, and it is the moment the overlap ban is enforced. There is no TenantId here — it is resolved
/// server-side from the claim.
/// <para><c>CycleStatus</c> is absent on purpose, so a status can never be set as a side effect of an edit.</para>
/// <para>FU07: <c>ScopeType</c> names the level and exactly ONE of the three references belongs to it. Sending a
/// second reference is refused rather than ignored — dropping a value the author typed would let them believe they
/// created a period they did not create.</para>
/// </summary>
public sealed record CreateCyclePeriodCommand(
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? ScopeType,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    string? Description,
    /// <summary>Informational only (business-unit scope): the country the author filtered by when choosing the unit.
    /// It is never part of the period's identity — see the entity field.
    /// <para>Optional and LAST so that adding it did not renumber the positional record: every caller written before it
    /// existed still compiles and still means exactly what it meant.</para></summary>
    string? BusinessUnitCountryContext = null) : IRequest<Response<Guid>>;
