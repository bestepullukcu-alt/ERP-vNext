using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContactAvailability.Queries;

/// <summary>Every availability row across all links of one contact, grouped by link/account.</summary>
public sealed record ListContactAvailabilityQuery(Guid ContactId) : IRequest<Response<IReadOnlyList<LinkAvailabilityDto>>>;

/// <summary>The location-scoped schedule + preference + exceptions of one AccountContactLink.</summary>
public sealed record GetLinkAvailabilityQuery(Guid AccountContactLinkId) : IRequest<Response<LinkAvailabilityDto>>;

/// <summary>Every contact's availability at one account/location.</summary>
public sealed record ListAccountContactAvailabilityQuery(Guid AccountId) : IRequest<Response<IReadOnlyList<LinkAvailabilityDto>>>;

/// <summary>
/// The MOD-0151 FU09A / MOD-0155 consumption seam: the effective window for a concrete date, with date-specific
/// exceptions already applied. Returns rows and reason codes — never a verdict, an ordering or a plan.
/// </summary>
public sealed record LookupContactAvailabilityQuery(
    string Date,
    Guid? ContactId = null,
    Guid? AccountId = null,
    Guid? AccountContactLinkId = null) : IRequest<Response<ContactAvailabilityLookupDto>>;
