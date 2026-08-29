using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Features.Account;

/// <summary>
/// Validates Account reference fields against MOD-0048 / PSS-012 published sets. No CRM local seed / hardcoded
/// fallback: a missing required set is surfaced as a controlled error (blocks create/update), never faked.
/// </summary>
public static class AccountReferenceValidation
{
    public const string AccountTypeSet = "account-type";
    public const string AccountStatusSet = "account-status";
    public const string AccountCategorySet = "account-category";

    public static async Task<IReadOnlyList<string>> ValidateAsync(
        IReferenceDataValidator validator,
        string accountType,
        string status,
        string? accountCategory,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        await CheckRequiredAsync(validator, AccountTypeSet, accountType, errors, cancellationToken);
        await CheckRequiredAsync(validator, AccountStatusSet, status, errors, cancellationToken);

        if (!string.IsNullOrWhiteSpace(accountCategory))
        {
            await CheckOptionalAsync(validator, AccountCategorySet, accountCategory!, errors, cancellationToken);
        }

        return errors;
    }

    private static async Task CheckRequiredAsync(
        IReferenceDataValidator validator, string setCode, string value, List<string> errors, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"'{setCode}' is required.");
            return;
        }

        var result = await validator.ValidateAsync(setCode, value, ct);
        switch (result.Status)
        {
            case ReferenceValidationStatus.InvalidValue:
                errors.Add($"'{value}' is not a valid published value of reference set '{setCode}'.");
                break;
            case ReferenceValidationStatus.SetMissing:
                errors.Add($"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).");
                break;
        }
    }

    private static async Task CheckOptionalAsync(
        IReferenceDataValidator validator, string setCode, string value, List<string> errors, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(setCode, value, ct);
        if (result.Status == ReferenceValidationStatus.InvalidValue)
        {
            errors.Add($"'{value}' is not a valid published value of reference set '{setCode}'.");
        }
        // SetMissing for an optional field is tolerated: the value is simply not validated until the set is published.
    }
}
