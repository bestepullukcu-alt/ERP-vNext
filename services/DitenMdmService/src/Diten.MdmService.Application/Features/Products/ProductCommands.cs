using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Products;

public sealed class CreateProductRequest : ProductUpsertRequestBase, IRequest<Guid>
{
}

public sealed class UpdateProductRequest : ProductUpsertRequestBase, IRequest<bool>
{
    public Guid Id { get; set; }
}

public sealed class ChangeProductLifecycleRequest : IRequest<bool>
{
    public Guid Id { get; set; }
    public Guid LifecycleStateId { get; set; }
}

public sealed class DeleteProductRequest : IRequest<bool>
{
    public Guid Id { get; set; }

    public DeleteProductRequest()
    {
    }

    public DeleteProductRequest(Guid id)
    {
        Id = id;
    }
}

public sealed class BulkDeleteProductsRequest : IRequest<BulkDeleteProductsResponse>
{
    public List<Guid> Ids { get; set; } = [];
}

public sealed class BulkDeleteProductsResponse
{
    public int DeletedCount { get; set; }
}
