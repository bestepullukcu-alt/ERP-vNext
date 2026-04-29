using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;
using FluentValidation;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Validators;

public sealed class UpdateGoldenReferenceSlimValidator : AbstractValidator<UpdateGoldenReferenceSlimCommand>
{
    public UpdateGoldenReferenceSlimValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
