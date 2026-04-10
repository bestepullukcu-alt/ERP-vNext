using MediatR;

namespace Diten.MdmService.Application.Features.PackagingDefinitions;

public sealed class CreatePackagingDefinitionRequest : PackagingDefinitionUpsertRequestBase, IRequest<Guid> { }

public sealed class UpdatePackagingDefinitionRequest : PackagingDefinitionUpsertRequestBase, IRequest<bool>
{
    public Guid Id { get; set; }
}

public sealed class ChangePackagingDefinitionLifecycleRequest : IRequest<bool>
{
    public Guid Id { get; set; }
    public Guid LifecycleStateId { get; set; }
}

public sealed class DeletePackagingDefinitionRequest : IRequest<bool>
{
    public Guid Id { get; set; }

    public DeletePackagingDefinitionRequest() { }

    public DeletePackagingDefinitionRequest(Guid id)
    {
        Id = id;
    }
}

public sealed class BulkDeletePackagingDefinitionsRequest : IRequest<BulkDeletePackagingDefinitionsResponse>
{
    public List<Guid> Ids { get; set; } = [];
}

public sealed class BulkDeletePackagingDefinitionsResponse
{
    public int DeletedCount { get; set; }
}
