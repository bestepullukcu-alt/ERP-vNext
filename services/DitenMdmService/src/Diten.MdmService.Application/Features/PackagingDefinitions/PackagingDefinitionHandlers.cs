using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.PackagingDefinitions.Handlers;

public sealed class CreatePackagingDefinitionRequestHandler : IRequestHandler<CreatePackagingDefinitionRequest, Guid>
{
    private readonly IPackagingDefinitionRepository _repository;

    public CreatePackagingDefinitionRequestHandler(IPackagingDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreatePackagingDefinitionRequest request, CancellationToken cancellationToken)
    {
        var entity = new PackagingDefinition
        {
            Code = request.Code,
            Name = request.Name,
            PackagingType = request.PackagingType,
            PackagingLevel = request.PackagingLevel,
            ChildPackagingId = request.ChildPackagingId,
            UnitsPerPack = request.UnitsPerPack,
            Dimensions = request.Dimensions,
            Weight = request.Weight,
            LifecycleStateId = request.LifecycleStateId
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return created.Id;
    }
}

public sealed class UpdatePackagingDefinitionRequestHandler : IRequestHandler<UpdatePackagingDefinitionRequest, bool>
{
    private readonly IPackagingDefinitionRepository _repository;

    public UpdatePackagingDefinitionRequestHandler(IPackagingDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdatePackagingDefinitionRequest request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing == null) throw new KeyNotFoundException("PackagingDefinition not found.");

        existing.Code = request.Code;
        existing.Name = request.Name;
        existing.PackagingType = request.PackagingType;
        existing.PackagingLevel = request.PackagingLevel;
        existing.ChildPackagingId = request.ChildPackagingId;
        existing.UnitsPerPack = request.UnitsPerPack;
        existing.Dimensions = request.Dimensions;
        existing.Weight = request.Weight;
        existing.LifecycleStateId = request.LifecycleStateId;

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}

public sealed class DeletePackagingDefinitionRequestHandler : IRequestHandler<DeletePackagingDefinitionRequest, bool>
{
    private readonly IPackagingDefinitionRepository _repository;

    public DeletePackagingDefinitionRequestHandler(IPackagingDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeletePackagingDefinitionRequest request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}

public sealed class ChangePackagingDefinitionLifecycleRequestHandler : IRequestHandler<ChangePackagingDefinitionLifecycleRequest, bool>
{
    private readonly IPackagingDefinitionRepository _repository;

    public ChangePackagingDefinitionLifecycleRequestHandler(IPackagingDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ChangePackagingDefinitionLifecycleRequest request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing == null) throw new KeyNotFoundException("PackagingDefinition not found.");

        existing.LifecycleStateId = request.LifecycleStateId;
        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}

public sealed class BulkDeletePackagingDefinitionsRequestHandler : IRequestHandler<BulkDeletePackagingDefinitionsRequest, BulkDeletePackagingDefinitionsResponse>
{
    private readonly IPackagingDefinitionRepository _repository;

    public BulkDeletePackagingDefinitionsRequestHandler(IPackagingDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<BulkDeletePackagingDefinitionsResponse> Handle(BulkDeletePackagingDefinitionsRequest request, CancellationToken cancellationToken)
    {
        var deletedCount = await _repository.BulkDeleteAsync(request.Ids, cancellationToken);
        return new BulkDeletePackagingDefinitionsResponse { DeletedCount = deletedCount };
    }
}

public sealed class GetAllPackagingDefinitionsQueryHandler : IRequestHandler<GetAllPackagingDefinitionsQuery, IReadOnlyList<PackagingDefinitionListItemDto>>
{
    private readonly IPackagingDefinitionRepository _repository;

    public GetAllPackagingDefinitionsQueryHandler(IPackagingDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PackagingDefinitionListItemDto>> Handle(GetAllPackagingDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        
        return items.Select(x => new PackagingDefinitionListItemDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            PackagingType = x.PackagingType,
            PackagingLevel = x.PackagingLevel,
            UnitsPerPack = x.UnitsPerPack,
            LifecycleStateId = x.LifecycleStateId
        }).ToList();
    }
}

public sealed class GetPackagingDefinitionByIdQueryHandler : IRequestHandler<GetPackagingDefinitionByIdQuery, PackagingDefinitionDetailDto?>
{
    private readonly IPackagingDefinitionRepository _repository;

    public GetPackagingDefinitionByIdQueryHandler(IPackagingDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<PackagingDefinitionDetailDto?> Handle(GetPackagingDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null) return null;

        return new PackagingDefinitionDetailDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            PackagingType = entity.PackagingType,
            PackagingLevel = entity.PackagingLevel,
            UnitsPerPack = entity.UnitsPerPack,
            LifecycleStateId = entity.LifecycleStateId,
            ChildPackagingId = entity.ChildPackagingId,
            Dimensions = entity.Dimensions,
            Weight = entity.Weight
        };
    }
}
