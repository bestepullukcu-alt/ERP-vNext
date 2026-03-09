using System;

namespace Diten.MdmService.Application.Features.LegalEntities.Requests;

public sealed record CreateLegalEntityRequest(
    string Title,
    string TaxOffice,
    string TaxNumber,
    string? Email,
    string? Phone,
    string? Website,
    string? Address,
    string? CompanyType,
    string? Sector,
    string? ContactPerson,
    string? PrimaryCurrency,
    string? DefaultTimeZone,
    Guid? ParentLegalEntityId,
    string? DefaultCommunicationLanguage,
    string? OrganizationRole,
    string? LogoUrl,
    string? FiscalYearStart,
    DateTimeOffset? RegistrationDate,
    DateTimeOffset? EffectiveFromDate,
    string? TaxJurisdiction
);
