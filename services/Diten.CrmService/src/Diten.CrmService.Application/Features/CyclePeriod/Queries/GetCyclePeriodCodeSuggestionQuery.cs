using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

/// <summary>
/// WP-CAP-MODEL (K-2) — "what code would the next period of this scope and year get?". A READ: it reserves nothing,
/// and the create command still takes its code from the request (uniqueness, lower-case storage and immutability
/// unchanged). The scope arguments are normalised exactly as a write's are.
/// </summary>
public sealed record GetCyclePeriodCodeSuggestionQuery(
    string? ScopeType,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    int Year) : IRequest<Response<CyclePeriodCodeSuggestionDto>>;
