using Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;
using FluentValidation;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Validators;

public sealed class UpdateGoldenReferenceItemValidator : AbstractValidator<UpdateGoldenReferenceItemCommand>
{
    public UpdateGoldenReferenceItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
