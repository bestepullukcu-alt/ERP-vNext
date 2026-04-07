using MediatR;

namespace Diten.MdmService.Application.Features.ItemCategories;

public sealed record GetAllItemCategoriesQuery : IRequest<IReadOnlyList<ItemCategoryDto>>;
public sealed record GetItemCategoryByIdQuery(Guid Id) : IRequest<ItemCategoryDto?>;

public sealed class CreateItemCategoryRequest : ItemCategoryUpsertRequestBase, IRequest<Guid>
{
}

public sealed class UpdateItemCategoryRequest : ItemCategoryUpsertRequestBase, IRequest<bool>
{
    public Guid Id { get; set; }
}

public sealed class DeleteItemCategoryRequest : IRequest<bool>
{
    public Guid Id { get; set; }

    public DeleteItemCategoryRequest()
    {
    }

    public DeleteItemCategoryRequest(Guid id)
    {
        Id = id;
    }
}

public sealed class BulkDeleteItemCategoriesRequest : IRequest<BulkDeleteItemCategoriesResponse>
{
    public List<Guid> Ids { get; set; } = [];
}
