using Diten.MdmService.Application.Features.Product.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.Product.Validators;

// Field-shape rules only; vocabulary/uniqueness/brand-link/archive rules live in the handlers so they can
// return their own reason code and status.
public sealed class ProductWriteRequestValidator : AbstractValidator<ProductWriteRequest>
{
    public ProductWriteRequestValidator()
    {
        RuleFor(x => x.ProductCode)
            .NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("ProductCode may contain letters, digits, dot, underscore and hyphen only.");

        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProductStatus).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Strength).MaximumLength(100);
        RuleFor(x => x.PackSize).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.EffectiveFrom).NotEmpty();

        // Shape check only — this is an EXTERNAL taxonomy pointer, so no ATC catalogue is consulted.
        RuleFor(x => x.ATCCode)
            .MaximumLength(16)
            .Matches("^[A-Za-z0-9]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.ATCCode))
            .WithMessage("ATCCode is an external taxonomy reference and must be alphanumeric.");

        RuleForEach(x => x.ExternalReferences).ChildRules(reference =>
        {
            reference.RuleFor(x => x.SourceSystem).NotEmpty().MaximumLength(100);
            reference.RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(200);
            reference.RuleFor(x => x.ExternalCode).MaximumLength(100);
            reference.RuleFor(x => x.ExternalName).MaximumLength(200);
        });
    }
}

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Request).NotNull().SetValidator(new ProductWriteRequestValidator());
    }
}
