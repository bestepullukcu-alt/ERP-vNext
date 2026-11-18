namespace Diten.CrmService.Application.Features.Account;

/// <summary>
/// MOD-0149 §30 (Treasury AR consumption) — the ONE place the CRM-ACCOUNT-LOOKUP 1.0 producer rules live. U-1
/// (<c>lookup-validation</c>) and U-3 (<c>lookup</c>) both build their rows here, so the two endpoints cannot disagree
/// on <c>isBillable</c> or <c>contractVersion</c> (AC-AR-9a).
/// </summary>
public static class AccountLookupContract
{
    /// <summary>Contract §2.2 / §2a.2.</summary>
    public const string ContractVersion = "crm-account-lookup/1.0";

    /// <summary>Contract §4: the only billable <c>account-status</c> code (Y-4/4c, K-2).</summary>
    public const string BillableStatus = "active";

    /// <summary>U-3 input bounds (contract §2a.1, pack AC-AR-12).</summary>
    public const int SearchMinLength = 2;

    public const int SearchMaxLength = 100;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    /// <summary>The single 404 text of U-1: missing, soft-deleted and other-tenant accounts answer the same bytes.</summary>
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
