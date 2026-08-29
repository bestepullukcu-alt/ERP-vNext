using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Commands;

/// <summary>
/// Edits a period. What may change depends on the lifecycle: a <c>draft</c> is fully editable, an <c>active</c> period
/// accepts only its name and description (moving a live period's days would silently re-date every plan that points at
/// it), and a <c>closed</c> period accepts nothing at all.
/// <para><c>CycleCode</c> is not here: it is the stable business key and is never renamed.</para>
/// <para>FU07: <c>ScopeType</c> is accepted so a round-tripping form is not punished, but it can never CHANGE — the
/// scope is half of the period's identity. A draft may still correct its scope REFERENCE (a mistyped country), which
/// is a different act from moving the period between levels.</para>
/// </summary>
public sealed record UpdateCyclePeriodCommand(
    Guid CyclePeriodId,
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
    int? ExpectedVersion,
    /// <summary>Informational only (business-unit scope): the country the author filtered by when choosing the unit.
    /// It is never part of the period's identity — see the entity field.
    /// <para>Optional and LAST so that adding it did not renumber the positional record: every caller written before it
    /// existed still compiles and still means exactly what it meant.</para></summary>
    string? BusinessUnitCountryContext = null) : IRequest<Response<bool>>;
