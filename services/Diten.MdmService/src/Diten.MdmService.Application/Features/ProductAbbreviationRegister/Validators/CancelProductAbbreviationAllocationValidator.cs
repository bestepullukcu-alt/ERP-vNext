using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Validators;

public sealed class CancelProductAbbreviationAllocationValidator
    : AbstractValidator<CancelProductAbbreviationAllocationCommand>
{
    public CancelProductAbbreviationAllocationValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}
