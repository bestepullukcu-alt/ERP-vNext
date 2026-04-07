using MediatR;

namespace Diten.MdmService.Application.Features.Items;

public sealed class CreateItemRequest : ItemUpsertRequestBase, IRequest<Guid>
{
}

public sealed class UpdateItemRequest : ItemUpsertRequestBase, IRequest<bool>
{
    public Guid Id { get; set; }
}

public sealed class PatchItemStatusRequest : IRequest<bool>
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}

public sealed class DeleteItemRequest : IRequest<bool>
{
    public Guid Id { get; set; }

    public DeleteItemRequest()
    {
    }

    public DeleteItemRequest(Guid id)
    {
        Id = id;
    }
}

public sealed class BulkDeleteItemsRequest : IRequest<BulkDeleteItemsResponse>
{
    public List<Guid> Ids { get; set; } = [];
}
