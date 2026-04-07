using FluentValidation;

namespace Diten.MdmService.Application.Features.Items;

public sealed class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemRequestValidator()
    {
        Include(new ItemUpsertValidator<CreateItemRequest>());
    }
}

public sealed class UpdateItemRequestValidator : AbstractValidator<UpdateItemRequest>
{
    public UpdateItemRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new ItemUpsertValidator<UpdateItemRequest>());
    }
}

public sealed class PatchItemStatusRequestValidator : AbstractValidator<PatchItemStatusRequest>
{
    public PatchItemStatusRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class BulkDeleteItemsRequestValidator : AbstractValidator<BulkDeleteItemsRequest>
{
    public BulkDeleteItemsRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
    }
}

internal sealed class ItemUpsertValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : ItemUpsertRequestBase
{
    public ItemUpsertValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ItemTypeId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.BaseUomId).NotEmpty();
        RuleFor(x => x.TrackingPolicyId).NotEmpty();
        RuleFor(x => x.LifecycleStateId).NotEmpty();
        RuleForEach(x => x.AttributeValues).ChildRules(child =>
        {
            child.RuleFor(x => x.AttributeDefinitionId).NotEmpty();
            child.RuleFor(x => x.Value).NotEmpty();
        });
        RuleForEach(x => x.Variants).ChildRules(child =>
        {
            child.RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
            child.RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
            child.RuleForEach(x => x.AttributeValues).ChildRules(value =>
            {
                value.RuleFor(x => x.AttributeDefinitionId).NotEmpty();
                value.RuleFor(x => x.Value).NotEmpty();
            });
        });
    }
}
