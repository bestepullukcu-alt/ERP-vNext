using FluentValidation;

namespace Diten.MdmService.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProductType).InclusiveBetween(1, 4);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.LifecycleStateId).NotEmpty();
    }
}
