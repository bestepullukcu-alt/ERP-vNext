using FluentValidation;
using Diten.MdmService.Application.Interfaces;

namespace Diten.MdmService.Application.Features.Skus;

public class SkuUpsertRequestValidator : AbstractValidator<SkuUpsertRequestBase>
{
    private readonly ISkuRepository _skuRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICompositionRepository _compositionRepository;

    public SkuUpsertRequestValidator(
        ISkuRepository skuRepository,
        IProductRepository productRepository,
        ICompositionRepository compositionRepository)
    {
        _skuRepository = skuRepository;
        _productRepository = productRepository;
        _compositionRepository = compositionRepository;

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("SKU Code is required")
            .MaximumLength(50).WithMessage("SKU Code cannot exceed 50 characters");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required")
            .MustAsync(async (productId, ct) => await _productRepository.GetByIdAsync(productId, ct) != null)
            .WithMessage("Invalid Product selected");

        RuleFor(x => x.CompositionId)
            .NotEmpty().WithMessage("Composition is required")
            .MustAsync(async (compositionId, ct) => await _compositionRepository.GetByIdAsync(compositionId, ct) != null)
            .WithMessage("Invalid Composition selected");

        RuleFor(x => x.PackagingForm)
            .NotEmpty().WithMessage("Packaging Form is required");

        RuleFor(x => x.PackagingQuantity)
            .GreaterThan(0).WithMessage("Packaging Quantity must be greater than zero");

        RuleFor(x => x.LifecycleStateId)
            .NotEmpty().WithMessage("Lifecycle State is required");
    }
}

public class CreateSkuRequestValidator : SkuUpsertRequestValidator
{
    public CreateSkuRequestValidator(
        ISkuRepository skuRepository,
        IProductRepository productRepository,
        ICompositionRepository compositionRepository) 
        : base(skuRepository, productRepository, compositionRepository)
    {
        RuleFor(x => x.Code)
            .MustAsync(async (code, ct) => !await skuRepository.ExistsByCodeAsync(code, null, ct))
            .WithMessage("SKU Code already exists in this tenant");
    }
}

public class UpdateSkuRequestValidator : SkuUpsertRequestValidator
{
    public UpdateSkuRequestValidator(
        ISkuRepository skuRepository,
        IProductRepository productRepository,
        ICompositionRepository compositionRepository,
        Guid skuId) 
        : base(skuRepository, productRepository, compositionRepository)
    {
        RuleFor(x => x.Code)
            .MustAsync(async (code, ct) => !await skuRepository.ExistsByCodeAsync(code, skuId, ct))
            .WithMessage("SKU Code already exists in this tenant");
    }
}
