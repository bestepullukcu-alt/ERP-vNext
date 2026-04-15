using FluentValidation;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.Skus;

internal sealed class SkuUpsertRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : SkuUpsertRequestBase
{
    public SkuUpsertRequestValidator(
        IItemRepository itemRepository,
        ICompositionRepository compositionRepository)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("SKU Code is required")
            .MaximumLength(50).WithMessage("SKU Code cannot exceed 50 characters");

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item is required")
            .MustAsync(async (itemId, ct) => await itemRepository.GetByIdAsync(itemId, ct) != null)
            .WithMessage("Invalid Item selected");

        RuleFor(x => x.CompositionId)
            .NotEmpty().WithMessage("Composition is required")
            .MustAsync(async (compositionId, ct) => await compositionRepository.GetByIdAsync(compositionId, ct) != null)
            .WithMessage("Invalid Composition selected");

        RuleFor(x => x.PackagingForm)
            .NotEmpty().WithMessage("Packaging Form is required");

        RuleFor(x => x.PackagingQuantity)
            .GreaterThan(0).WithMessage("Packaging Quantity must be greater than zero");

        RuleFor(x => x.LifecycleStateId)
            .NotEmpty().WithMessage("Lifecycle State is required");
    }
}

public sealed class CreateSkuRequestValidator : AbstractValidator<CreateSkuCommand>
{
    public CreateSkuRequestValidator(
        ISkuRepository skuRepository,
        IItemRepository itemRepository,
        ICompositionRepository compositionRepository)
    {
        Include(new SkuUpsertRequestValidator<CreateSkuCommand>(itemRepository, compositionRepository));

        RuleFor(x => x.Code)
            .MustAsync(async (code, ct) => !await skuRepository.ExistsByCodeAsync(code, null, ct))
            .WithMessage("SKU Code already exists in this tenant");
    }
}

public sealed class UpdateSkuRequestValidator : AbstractValidator<UpdateSkuCommand>
{
    public UpdateSkuRequestValidator(
        ISkuRepository skuRepository,
        IItemRepository itemRepository,
        ICompositionRepository compositionRepository)
    {
        RuleFor(x => x.Id).NotEmpty();

        Include(new SkuUpsertRequestValidator<UpdateSkuCommand>(itemRepository, compositionRepository));

        RuleFor(x => x.Code)
            .MustAsync(async (request, code, ct) => !await skuRepository.ExistsByCodeAsync(code, request.Id, ct))
            .WithMessage("SKU Code already exists in this tenant");
    }
}
