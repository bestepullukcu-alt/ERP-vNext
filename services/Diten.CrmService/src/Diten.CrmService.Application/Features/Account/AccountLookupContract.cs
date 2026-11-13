namespace Diten.CrmService.Application.Features.Account;

/// <summary>
/// MOD-0149 §30 (Treasury AR consumption) — the ONE place the CRM-ACCOUNT-LOOKUP 1.0 producer rules live. U-1
/// (<c>lookup-validation</c>) and U-3 (<c>lookup</c>) both build their rows here, so the two endpoints cannot disagree
/// on <c>isBillable</c> or <c>contractVersion</c> (AC-AR-9a). §32 U-4 (<c>ship-to</c>, contract 1.1) builds its body
/// here too and shares U-1's 400 / 404 / 503 texts.
/// </summary>
public static class AccountLookupContract
{
    /// <summary>Contract §2.2 / §2a.2.</summary>
    public const string ContractVersion = "crm-account-lookup/1.0";

    /// <summary>MOD-0149 §32 U-4 (CRM-ACCOUNT-LOOKUP 1.1 §2.2): each endpoint reports the minor version its own body was
    /// defined in. U-1 / U-3 stay on <see cref="ContractVersion"/>.</summary>
    public const string ShipToContractVersion = "crm-account-lookup/1.1";

    /// <summary>U-4: the fixed <c>addressId</c> and <c>kind</c> of the account's own (main) address.</summary>
    public const string PrimaryAddress = "primary";

    /// <summary>Contract §4: the only billable <c>account-status</c> code (Y-4/4c, K-2).</summary>
    public const string BillableStatus = "active";

    /// <summary>U-3 input bounds (contract §2a.1, pack AC-AR-12).</summary>
    public const int SearchMinLength = 2;

    public const int SearchMaxLength = 100;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    /// <summary>The single 404 text of U-1 and U-4: missing, soft-deleted and other-tenant accounts answer the same bytes.</summary>
    public const string NotFoundMessage = "Account not found.";

    public const string TenantRequiredMessage = "Tenant context is required.";

    public const string StoreUnavailableMessage = "The account store cannot be read. No result is given.";

    /// <summary>Contract §2.2: <c>status == "active"</c>, ordinal (case-sensitive), and nothing else is consulted.</summary>
    public static bool IsBillable(string? status) => string.Equals(status, BillableStatus, StringComparison.Ordinal);

    public static AccountLookupValidationDto ToValidation(Domain.Entities.Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new AccountLookupValidationDto(
            account.Id, account.AccountCode, account.AccountName, account.Status, IsBillable(account.Status), ContractVersion);
    }

    public static AccountLookupItemDto ToItem(Domain.Entities.Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new AccountLookupItemDto(
            account.Id, account.AccountCode, account.AccountName, account.Status, IsBillable(account.Status));
    }

    /// <summary>
    /// MOD-0149 §32.2 U-4: the account's main address as a list. The four fields are trimmed and blank ones become null;
    /// when at least one is filled the list holds the <c>primary</c> entry (the default), otherwise it is empty and
    /// there is no default. <c>addressLine</c> is never cut. Nothing else of the account is carried: no responsible
    /// person, note, parent, external reference, attribute, logo, location or <c>isBillable</c>.
    /// </summary>
    public static AccountShipToDto ToShipTo(Domain.Entities.Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var country = TrimToNull(account.CountryRef);
        var city = TrimToNull(account.CityRef);
        var district = TrimToNull(account.DistrictRef);
        var line = TrimToNull(account.AddressLine);

        IReadOnlyList<AccountShipToAddressDto> addresses = country is null && city is null && district is null && line is null
            ? []
            :
            [
                new AccountShipToAddressDto(
                    PrimaryAddress, PrimaryAddress, Label: null, IsDefault: true, country, city, district, PostalCode: null, line)
            ];

        return new AccountShipToDto(
            account.Id,
            account.AccountCode,
            account.AccountName,
            account.Status,
            addresses.FirstOrDefault(a => a.IsDefault)?.AddressId,
            addresses,
            ShipToContractVersion);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Contract §7: the store could not be read, i.e. a Mongo driver failure (any <c>MongoDB.Driver.MongoException</c>) or
    /// a timeout (server selection raises <see cref="TimeoutException"/>). The Application project carries no Mongo
    /// reference, so the driver's base type is recognised by name along the exception's type chain.
    /// </summary>
    public static bool IsStoreUnavailable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is TimeoutException)
        {
            return true;
        }

        for (var type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (string.Equals(type.FullName, "MongoDB.Driver.MongoException", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
