using FluentValidation;

namespace Diten.MdmService.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProductType).InclusiveBetween(1, 4);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.LifecycleStateId).NotEmpty();
    }
}
