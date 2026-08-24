using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Validators;

public sealed class InitiateProductAbbreviationCorrectionValidator
    : AbstractValidator<InitiateProductAbbreviationCorrectionCommand>
{
    public InitiateProductAbbreviationCorrectionValidator()
    {
        RuleFor(x => x.ActiveRegisterEntryId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReplacementAbbreviation)
            .Must(value => ProductAbbreviationNormalizer.TryNormalize(value, out _))
            .WithMessage("ABBREVIATION_GRAMMAR_INVALID");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}
