using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;
using FluentValidation;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Validators;

public sealed class CreateGoldenReferenceSlimValidator : AbstractValidator<CreateGoldenReferenceSlimCommand>
{
    public CreateGoldenReferenceSlimValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
