using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Validators;

public sealed class ApproveProductAbbreviationAllocationValidator
    : AbstractValidator<ApproveProductAbbreviationAllocationCommand>
{
    public ApproveProductAbbreviationAllocationValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedFormerVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedFormerVersion.HasValue);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}
