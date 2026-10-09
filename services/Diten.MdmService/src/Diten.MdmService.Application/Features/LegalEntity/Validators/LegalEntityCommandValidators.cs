using Diten.MdmService.Application.Features.LegalEntity.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.LegalEntity.Validators;

// Both write commands delegate to the shared request validator so Create and Update enforce identical field rules.
public sealed class CreateLegalEntityCommandValidator : AbstractValidator<CreateLegalEntityCommand>
{
    public CreateLegalEntityCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().SetValidator(new LegalEntityWriteRequestValidator());
        RuleFor(x => x.Request.ExpectedVersion)
            .Null()
            .When(x => x.Request is not null)
            .WithMessage("ExpectedVersion is not accepted when creating a Legal Entity.");
    }
}

public sealed class UpdateLegalEntityCommandValidator : AbstractValidator<UpdateLegalEntityCommand>
{
    public UpdateLegalEntityCommandValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new LegalEntityWriteRequestValidator());
        RuleFor(x => x.Request.ExpectedVersion)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request is not null)
            .WithMessage("ExpectedVersion must be a non-negative integer when updating a Legal Entity.");
    }
}
