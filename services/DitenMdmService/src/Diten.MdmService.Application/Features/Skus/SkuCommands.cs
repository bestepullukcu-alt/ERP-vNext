using MediatR;

namespace Diten.MdmService.Application.Features.Skus;

public sealed class CreateSkuCommand : SkuUpsertRequestBase, IRequest<Guid> { }

public sealed class UpdateSkuCommand : SkuUpsertRequestBase, IRequest<bool>
{
    public Guid Id { get; set; }
}

public sealed record DeleteSkuCommand(Guid Id) : IRequest<bool>;

public sealed record BulkDeleteSkusCommand(List<Guid> Ids) : IRequest<int>;
