using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.BankMaster;

/// <summary>
/// The rules of a branch that need its bank (the field-level ones are the validator's): the country equals the bank's, and a
/// BIC carries the branch's country and the bank's own first 8 characters. Returns the error code, or null when it fits.
/// </summary>
internal static class BankBranchRules
{
    public static string? Judge(Bank bank, BankBranchWriteRequest request)
    {
        var country = BankRules.NormalizeCountry(request.CountryCode);
        if (country is not null && country != bank.CountryCode) return BankErrorCodes.ValidationFailed;

        var bic = BankRules.NormalizeBic(request.Bic);
        if (bic is null) return null;
        if (bic.Substring(4, 2) != bank.CountryCode) return BankErrorCodes.ValidationFailed;
        return BankRules.BranchBicMatchesBank(bic, bank.Bic) ? null : BankErrorCodes.ValidationFailed;
    }
}
