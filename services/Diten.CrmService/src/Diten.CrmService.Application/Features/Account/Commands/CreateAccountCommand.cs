using Diten.BuildingBlocks.Auditing;
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
    string? LogoDataUri = null) : IRequest<Response<Guid>>, IAuditableCommand, IAuditDetailsProvider
{
    // WP-CRM-MOBILE-REQ CRM-ACC-EDIT — audited (AUD-001, MOD-0149); the logo image never reaches the record.
    public AuditDescriptor DescribeAudit() => AccountAudit.Describe(AuditOperation.Create, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.EntityId = (response as Response<Guid>)?.Data is { } id && id != Guid.Empty ? id : null;
        details.Set("accountCode", AccountCode).Set("accountType", AccountType).Set("status", Status).Set("parentAccountId", ParentAccountId)
            .Set("externalReference", ExternalReference is { } r ? $"{r.SourceSystem}:{r.ExternalId}" : null).Set("logo", !string.IsNullOrWhiteSpace(LogoDataUri));
    }
}
