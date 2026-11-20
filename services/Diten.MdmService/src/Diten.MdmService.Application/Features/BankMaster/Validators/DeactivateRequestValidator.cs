using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

public sealed class DeactivateRequestValidator : AbstractValidator<DeactivateRequest>
{
    public DeactivateRequestValidator()
    {
        RuleFor(x => x.ReasonCode).Must(reason =>
        {
            var normalized = BankRules.NormalizeText(reason);
            return normalized is not null && normalized.Length <= BankRules.ReasonCodeMaxLength
                   && !BankRules.HasControlCharacters(normalized);
        }).WithMessage(BankValidationMessages.ReasonCodeInvalid);
    }
}
