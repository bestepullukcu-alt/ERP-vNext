using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Validators;

public sealed class RejectProductAbbreviationRetirementValidator
    : AbstractValidator<RejectProductAbbreviationRetirementCommand>
{
    public RejectProductAbbreviationRetirementValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RetirementRequestId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}
