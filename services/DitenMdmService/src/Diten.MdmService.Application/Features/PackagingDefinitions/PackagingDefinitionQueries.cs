using MediatR;

namespace Diten.MdmService.Application.Features.PackagingDefinitions;

public sealed class GetAllPackagingDefinitionsQuery : IRequest<IReadOnlyList<PackagingDefinitionListItemDto>> { }

public sealed class GetPackagingDefinitionByIdQuery : IRequest<PackagingDefinitionDetailDto?>
{
    public Guid Id { get; set; }

    public GetPackagingDefinitionByIdQuery() { }

    public GetPackagingDefinitionByIdQuery(Guid id)
    {
        Id = id;
    }
}
