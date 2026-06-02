using Diten.MdmService.Application.Features.LegalEntity.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.LegalEntity.Validators;

public sealed class CreateLegalEntityValidator : AbstractValidator<CreateLegalEntityCommand>
{
    public CreateLegalEntityValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.DisplayName)
            .MaximumLength(256);
    }
}
