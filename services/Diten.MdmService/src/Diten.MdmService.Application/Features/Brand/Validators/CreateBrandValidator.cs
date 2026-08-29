using Diten.MdmService.Application.Features.Brand.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.Brand.Validators;

// Field-shape rules only. Vocabulary, uniqueness, immutability, archive state and the effective window are
// business rules and stay in the handlers, where they can return their own reason code and status.
public sealed class BrandWriteRequestValidator : AbstractValidator<BrandWriteRequest>
{
    public BrandWriteRequestValidator()
    {
        RuleFor(x => x.BrandCode)
            .NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("BrandCode may contain letters, digits, dot, underscore and hyphen only.");

        RuleFor(x => x.BrandName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BrandStatus).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.EffectiveFrom).NotEmpty();

        RuleForEach(x => x.ExternalReferences).ChildRules(reference =>
        {
            reference.RuleFor(x => x.SourceSystem).NotEmpty().MaximumLength(100);
            reference.RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(200);
            reference.RuleFor(x => x.ExternalCode).MaximumLength(100);
            reference.RuleFor(x => x.ExternalName).MaximumLength(200);
        });
    }
}

public sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator()
    {
        RuleFor(x => x.Request).NotNull().SetValidator(new BrandWriteRequestValidator());
    }
}
