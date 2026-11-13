using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Commands;

public sealed record CreateAccountCommand(
    string AccountName,
    string? AccountCode,
    string AccountType,
    string? AccountCategory,
    Guid? ParentAccountId,
    string Status,
    string? CountryRef,
    string? CityRef,
    string? DistrictRef,
    string? AddressLine,
    double? Latitude,
    double? Longitude,
    string? ResponsiblePersonName,
    string? ResponsiblePersonPhone,
    string? ResponsiblePersonEmail,
    string? Notes,
    ExternalReferenceInput? ExternalReference,
    string? LogoDataUri = null) : IRequest<Response<Guid>>, ICentralAuditDeclaration
{
    // The record names the account, never its data (the logo image, names and addresses stay out; AUD-001 §3).
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(AccountAudit.Entity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<Guid>(response, id => id);
}
