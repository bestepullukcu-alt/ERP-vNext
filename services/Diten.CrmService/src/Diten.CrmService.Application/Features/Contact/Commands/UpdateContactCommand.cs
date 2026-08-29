using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Contact.Commands;

public sealed record UpdateContactCommand(
    Guid Id,
    string FirstName,
    string? LastName,
    string? DisplayName,
    string ContactType,
    string Status,
    string? ProfessionalTitle,
    string? Specialty,
    string? Department,
    string? Phone,
    string? Email,
    string? Notes,
    // MOD-0150 Contact Location Hardening — all optional; Country is never required.
    string? CountryRef = null,
    string? CityRef = null,
    string? DistrictRef = null,
    string? AddressLine = null,
    string? PostalCode = null,
    string? PreferredLanguage = null,
    string? PhoneCountryCode = null,
    string? Gender = null,
    string? PhotoDataUri = null) : IRequest<Response<bool>>;
