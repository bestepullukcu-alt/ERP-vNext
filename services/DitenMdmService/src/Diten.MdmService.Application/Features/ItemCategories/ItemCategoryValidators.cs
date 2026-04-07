using FluentValidation;

namespace Diten.MdmService.Application.Features.ItemCategories;

public sealed class CreateItemCategoryRequestValidator : AbstractValidator<CreateItemCategoryRequest>
{
    public CreateItemCategoryRequestValidator()
    {
        Include(new ItemCategoryUpsertValidator<CreateItemCategoryRequest>());
    }
}

public sealed class UpdateItemCategoryRequestValidator : AbstractValidator<UpdateItemCategoryRequest>
{
    public UpdateItemCategoryRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new ItemCategoryUpsertValidator<UpdateItemCategoryRequest>());
    }
}

public sealed class BulkDeleteItemCategoriesRequestValidator : AbstractValidator<BulkDeleteItemCategoriesRequest>
{
    public BulkDeleteItemCategoriesRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
    }
}

internal sealed class ItemCategoryUpsertValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : ItemCategoryUpsertRequestBase
{
    public ItemCategoryUpsertValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ItemTypeId).NotEmpty();
    }
}
