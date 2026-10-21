using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Commands;

/// <summary>
/// MOD-0149 account update. WP-CRM-MOBILE-REQ CRM-ACC-EDIT: <see cref="ExpectedVersion"/> is REQUIRED (the version the
/// caller read; a stale one is 409 <c>account_version_conflict</c> and nothing changes — §19, no silent overwrite); an
/// omitted <see cref="LogoDataUri"/> KEEPS the logo, <see cref="RemoveLogo"/> removes it. The fields this command does not
/// carry (code, parent, attributes, external references …) are never touched. Audited (AUD-001, MOD-0149).
/// </summary>
public sealed record UpdateAccountCommand(
    Guid Id,
    string AccountName,
    string AccountType,
    string? AccountCategory,
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
    string? LogoDataUri = null,
    int? ExpectedVersion = null,
    bool RemoveLogo = false) : IRequest<Response<bool>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => AccountAudit.Describe(AuditOperation.Update, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.ObjectVersion = ExpectedVersion is { } v && (response as Response<bool>)?.IsSuccessful == true ? v + 1 : null;
        details.Set("expectedVersion", ExpectedVersion).Set("accountType", AccountType).Set("status", Status)
            .Set("logo", RemoveLogo ? "removed" : string.IsNullOrWhiteSpace(LogoDataUri) ? "kept" : "replaced");
    }
}
