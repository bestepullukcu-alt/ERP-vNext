using FluentValidation;

namespace Diten.MdmService.Application.Features.Products;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        Include(new ProductUpsertRequestValidator());
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new ProductUpsertRequestValidator());
    }
}

public sealed class ChangeProductLifecycleRequestValidator : AbstractValidator<ChangeProductLifecycleRequest>
{
    public ChangeProductLifecycleRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LifecycleStateId).NotEmpty();
    }
}

public sealed class BulkDeleteProductsRequestValidator : AbstractValidator<BulkDeleteProductsRequest>
{
    public BulkDeleteProductsRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
    }
}

internal sealed class ProductUpsertRequestValidator : AbstractValidator<ProductUpsertRequestBase>
{
    public ProductUpsertRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.LifecycleStateId).NotEmpty();
        RuleFor(x => x.ProductType).IsInEnum();
    }
}
