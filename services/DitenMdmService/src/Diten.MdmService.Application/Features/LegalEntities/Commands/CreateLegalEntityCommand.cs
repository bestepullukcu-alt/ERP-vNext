using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntities.Commands;

public sealed record CreateLegalEntityCommand(
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
) : IRequest<CreateLegalEntityResult>;

public sealed record CreateLegalEntityResult(
    Guid Id,
    string Title,
    string TaxNumber,
    Guid TenantId,
    DateTimeOffset CreatedAt
);
