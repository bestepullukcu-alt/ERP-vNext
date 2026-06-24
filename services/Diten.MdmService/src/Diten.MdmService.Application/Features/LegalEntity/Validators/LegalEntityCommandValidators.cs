using Diten.MdmService.Application.Features.LegalEntity.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.LegalEntity.Validators;

// Both write commands delegate to the shared request validator so Create and Update enforce identical field rules.
public sealed class CreateLegalEntityCommandValidator : AbstractValidator<CreateLegalEntityCommand>
{
    public CreateLegalEntityCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().SetValidator(new LegalEntityWriteRequestValidator());
    }
}

public sealed class UpdateLegalEntityCommandValidator : AbstractValidator<UpdateLegalEntityCommand>
{
    public UpdateLegalEntityCommandValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new LegalEntityWriteRequestValidator());
    }
}
