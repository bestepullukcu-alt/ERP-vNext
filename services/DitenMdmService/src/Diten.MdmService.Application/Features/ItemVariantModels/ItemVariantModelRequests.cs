using MediatR;

namespace Diten.MdmService.Application.Features.ItemVariantModels;

public sealed record GetAllItemVariantModelsQuery : IRequest<IReadOnlyList<ItemVariantModelDto>>;
public sealed record GetItemVariantModelByIdQuery(Guid Id) : IRequest<ItemVariantModelDto?>;

public sealed class CreateItemVariantModelRequest : ItemVariantModelUpsertRequestBase, IRequest<Guid>
{
}

public sealed class UpdateItemVariantModelRequest : ItemVariantModelUpsertRequestBase, IRequest<bool>
{
    public Guid Id { get; set; }
}

public sealed class DeleteItemVariantModelRequest : IRequest<bool>
{
    public Guid Id { get; set; }

    public DeleteItemVariantModelRequest()
    {
    }

    public DeleteItemVariantModelRequest(Guid id)
    {
        Id = id;
    }
}

public sealed class BulkDeleteItemVariantModelsRequest : IRequest<BulkDeleteItemVariantModelsResponse>
{
    public List<Guid> Ids { get; set; } = [];
}
