using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Validators;

public sealed class RequestProductAbbreviationAllocationValidator
    : AbstractValidator<RequestProductAbbreviationAllocationCommand>
{
    public RequestProductAbbreviationAllocationValidator()
    {
        RuleFor(x => x.GlobalProductId).NotEmpty();
        RuleFor(x => x.Abbreviation)
            .Must(value => ProductAbbreviationNormalizer.TryNormalize(value, out _))
            .WithMessage("ABBREVIATION_GRAMMAR_INVALID");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
