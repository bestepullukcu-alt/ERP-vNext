namespace Diten.MdmService.Application.Features.BankMaster.Validators;

/// <summary>
/// The validator messages: a fixed resx key each (the response carries the key, the screen translates it). No message holds
/// <c>{PropertyValue}</c> or any other echo of what the caller sent.
/// </summary>
public static class BankValidationMessages
{
    private const string P = BankErrorCodes.ValidationFailed + ".";

    public const string RequestRequired = P + "REQUEST_REQUIRED";
    public const string TenantForbidden = P + "TENANT_FORBIDDEN";
    public const string NameInvalid = P + "NAME_INVALID";
    public const string CountryInvalid = P + "COUNTRY_INVALID";
    public const string BicInvalid = P + "BIC_INVALID";
    public const string NationalCodeInvalid = P + "NATIONAL_CODE_INVALID";
    public const string BranchCodeInvalid = P + "BRANCH_CODE_INVALID";
    public const string BranchBicInvalid = P + "BRANCH_BIC_INVALID";
    public const string CityInvalid = P + "CITY_INVALID";
    public const string SearchTooLong = P + "SEARCH_TOO_LONG";
    public const string StatusInvalid = P + "STATUS_INVALID";
    public const string SortInvalid = P + "SORT_INVALID";
    public const string PagingInvalid = P + "PAGING_INVALID";
    public const string ReasonCodeInvalid = P + "REASON_CODE_INVALID";
    public const string IdsInvalid = P + "IDS_INVALID";
}
