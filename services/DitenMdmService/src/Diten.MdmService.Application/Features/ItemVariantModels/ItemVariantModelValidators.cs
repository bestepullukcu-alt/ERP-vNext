using FluentValidation;

namespace Diten.MdmService.Application.Features.ItemVariantModels;

public sealed class CreateItemVariantModelRequestValidator : AbstractValidator<CreateItemVariantModelRequest>
{
    public CreateItemVariantModelRequestValidator()
    {
        Include(new ItemVariantModelUpsertValidator<CreateItemVariantModelRequest>());
    }
}

public sealed class UpdateItemVariantModelRequestValidator : AbstractValidator<UpdateItemVariantModelRequest>
{
    public UpdateItemVariantModelRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new ItemVariantModelUpsertValidator<UpdateItemVariantModelRequest>());
    }
}

public sealed class BulkDeleteItemVariantModelsRequestValidator : AbstractValidator<BulkDeleteItemVariantModelsRequest>
{
    public BulkDeleteItemVariantModelsRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
    }
}

internal sealed class ItemVariantModelUpsertValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : ItemVariantModelUpsertRequestBase
{
    public ItemVariantModelUpsertValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ItemTypeId).NotEmpty();
        RuleForEach(x => x.Attributes).ChildRules(child =>
        {
            child.RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
            child.RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
            child.RuleFor(x => x.DataType).NotEmpty().MaximumLength(32);
        });
    }
}
