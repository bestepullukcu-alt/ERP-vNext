using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

/// <summary>Field rules shared by Create and Update of a bank (pack "Validation Rules").</summary>
public sealed class BankWriteRequestValidator : AbstractValidator<BankWriteRequest>
{
    public BankWriteRequestValidator()
    {
        // A code in the request is refused, not ignored: the code is the shared service's (RCS-001).
        RuleFor(x => x.Code).Must(code => code is null).WithMessage(BankErrorCodes.CodeForbidden);
        RuleFor(x => x.TenantId).Must(tenant => tenant is null).WithMessage(BankValidationMessages.TenantForbidden);

        RuleFor(x => x.Name).Must(name =>
        {
            var normalized = BankRules.NormalizeText(name);
            return normalized is not null && normalized.Length <= BankRules.NameMaxLength && !BankRules.HasControlCharacters(normalized);
        }).WithMessage(BankValidationMessages.NameInvalid);

        RuleFor(x => x.CountryCode)
            .Must(country => BankRules.IsValidCountry(BankRules.NormalizeCountry(country)))
            .WithMessage(BankValidationMessages.CountryInvalid);

        // The BIC must fit ISO 9362 AND carry the bank's country in characters 5-6; a country that is itself invalid is
        // reported on its own rule, so the BIC is judged only against a valid country.
        RuleFor(x => x.Bic)
            .Must((request, bic) => BankRules.IsValidBic(
                BankRules.NormalizeBic(bic), BankRules.NormalizeCountry(request.CountryCode)))
            .When(x => BankRules.NormalizeBic(x.Bic) is not null && BankRules.IsValidCountry(BankRules.NormalizeCountry(x.CountryCode)))
            .WithMessage(BankValidationMessages.BicInvalid);

        RuleFor(x => x.NationalBankCode)
            .Must(code => BankRules.IsValidCode(BankRules.NormalizeCode(code)))
            .When(x => BankRules.NormalizeCode(x.NationalBankCode) is not null)
            .WithMessage(BankValidationMessages.NationalCodeInvalid);
    }
}
