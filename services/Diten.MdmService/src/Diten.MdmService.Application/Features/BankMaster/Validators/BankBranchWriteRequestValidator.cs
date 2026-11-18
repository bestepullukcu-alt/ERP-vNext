using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

/// <summary>
/// Field rules shared by Create and Update of a branch. What needs the bank (country equal, BIC's first 8 equal the bank's,
/// the bank of the route) is judged by the handler, which has read it.
/// </summary>
public sealed class BankBranchWriteRequestValidator : AbstractValidator<BankBranchWriteRequest>
{
    public BankBranchWriteRequestValidator()
    {
        RuleFor(x => x.TenantId).Must(tenant => tenant is null).WithMessage(BankValidationMessages.TenantForbidden);

        RuleFor(x => x.BranchCode)
            .Must(code => BankRules.IsValidCode(BankRules.NormalizeCode(code)))
            .WithMessage(BankValidationMessages.BranchCodeInvalid);

        RuleFor(x => x.Name).Must(name =>
        {
            var normalized = BankRules.NormalizeText(name);
            return normalized is not null && normalized.Length <= BankRules.NameMaxLength && !BankRules.HasControlCharacters(normalized);
        }).WithMessage(BankValidationMessages.NameInvalid);

        RuleFor(x => x.CountryCode)
            .Must(country => BankRules.IsValidCountry(BankRules.NormalizeCountry(country)))
            .When(x => BankRules.NormalizeCountry(x.CountryCode) is not null)
            .WithMessage(BankValidationMessages.CountryInvalid);

        RuleFor(x => x.City)
            .Must(city =>
            {
                var normalized = BankRules.NormalizeText(city);
                return normalized!.Length <= BankRules.CityMaxLength && !BankRules.HasControlCharacters(normalized);
            })
            .When(x => BankRules.NormalizeText(x.City) is not null)
            .WithMessage(BankValidationMessages.CityInvalid);

        // A branch BIC is the 11-character form of ISO 9362; its country is judged with the bank's country by the handler.
        RuleFor(x => x.Bic)
            .Must(bic => BankRules.IsBranchBic(BankRules.NormalizeBic(bic))
                         && BankRules.IsValidBic(BankRules.NormalizeBic(bic), BankRules.NormalizeBic(bic)!.Substring(4, 2)))
            .When(x => BankRules.NormalizeBic(x.Bic) is not null)
            .WithMessage(BankValidationMessages.BranchBicInvalid);
    }
}
