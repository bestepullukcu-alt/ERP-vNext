using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemVariantModels;

public sealed class GetAllItemVariantModelsQueryHandler : IRequestHandler<GetAllItemVariantModelsQuery, IReadOnlyList<ItemVariantModelDto>>
{
    private readonly IItemVariantModelRepository _repository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetAllItemVariantModelsQueryHandler(IItemVariantModelRepository repository, IItemLookupRepository lookupRepository)
    {
        _repository = repository;
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<ItemVariantModelDto>> Handle(GetAllItemVariantModelsQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var models = await _repository.GetAllAsync(cancellationToken);
        var itemTypes = await _lookupRepository.GetItemTypesAsync(cancellationToken);
        var itemTypeMap = itemTypes.ToDictionary(x => x.Id);

        var result = new List<ItemVariantModelDto>(models.Count);
        foreach (var model in models)
        {
            var templates = await _repository.GetTemplatesAsync(model.Id, cancellationToken);
            var definitions = await _repository.GetAttributeDefinitionsAsync(templates.Select(x => x.AttributeDefinitionId), cancellationToken);
            result.Add(ItemVariantModelMapping.ToDto(model, templates, definitions, itemTypeMap));
        }

        return result;
    }
}

public sealed class GetItemVariantModelByIdQueryHandler : IRequestHandler<GetItemVariantModelByIdQuery, ItemVariantModelDto?>
{
    private readonly IItemVariantModelRepository _repository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetItemVariantModelByIdQueryHandler(IItemVariantModelRepository repository, IItemLookupRepository lookupRepository)
    {
        _repository = repository;
        _lookupRepository = lookupRepository;
    }

    public async Task<ItemVariantModelDto?> Handle(GetItemVariantModelByIdQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var model = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (model is null)
        {
            return null;
        }

        var templates = await _repository.GetTemplatesAsync(model.Id, cancellationToken);
        var definitions = await _repository.GetAttributeDefinitionsAsync(templates.Select(x => x.AttributeDefinitionId), cancellationToken);
        var itemTypes = await _lookupRepository.GetItemTypesAsync(cancellationToken);
        return ItemVariantModelMapping.ToDto(model, templates, definitions, itemTypes.ToDictionary(x => x.Id));
    }
}

public sealed class CreateItemVariantModelRequestHandler : IRequestHandler<CreateItemVariantModelRequest, Guid>
{
    private readonly IItemVariantModelRepository _repository;
    private readonly IItemLookupRepository _lookupRepository;

    public CreateItemVariantModelRequestHandler(IItemVariantModelRepository repository, IItemLookupRepository lookupRepository)
    {
        _repository = repository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Guid> Handle(CreateItemVariantModelRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        await ItemVariantModelValidation.ValidateAsync(request, null, _repository, _lookupRepository, cancellationToken);

        var entity = new ItemVariantModel
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ItemTypeId = request.ItemTypeId,
            IsActive = request.IsActive
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        await ItemVariantModelValidation.SaveTemplatesAsync(created.Id, request.Attributes, _repository, cancellationToken);
        return created.Id;
    }
}

public sealed class UpdateItemVariantModelRequestHandler : IRequestHandler<UpdateItemVariantModelRequest, bool>
{
    private readonly IItemVariantModelRepository _repository;
    private readonly IItemLookupRepository _lookupRepository;

    public UpdateItemVariantModelRequestHandler(IItemVariantModelRepository repository, IItemLookupRepository lookupRepository)
    {
        _repository = repository;
        _lookupRepository = lookupRepository;
    }

    public async Task<bool> Handle(UpdateItemVariantModelRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Variant model not found.");

        await ItemVariantModelValidation.ValidateAsync(request, request.Id, _repository, _lookupRepository, cancellationToken);

        existing.Code = request.Code.Trim();
        existing.Name = request.Name.Trim();
        existing.Description = request.Description?.Trim();
        existing.ItemTypeId = request.ItemTypeId;
        existing.IsActive = request.IsActive;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);
        if (!updated)
        {
            return false;
        }

        await ItemVariantModelValidation.SaveTemplatesAsync(existing.Id, request.Attributes, _repository, cancellationToken);
        return true;
    }
}

public sealed class DeleteItemVariantModelRequestHandler : IRequestHandler<DeleteItemVariantModelRequest, bool>
{
    private readonly IItemVariantModelRepository _repository;

    public DeleteItemVariantModelRequestHandler(IItemVariantModelRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteItemVariantModelRequest request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}

public sealed class BulkDeleteItemVariantModelsRequestHandler : IRequestHandler<BulkDeleteItemVariantModelsRequest, BulkDeleteItemVariantModelsResponse>
{
    private readonly IItemVariantModelRepository _repository;

    public BulkDeleteItemVariantModelsRequestHandler(IItemVariantModelRepository repository)
    {
        _repository = repository;
    }

    public async Task<BulkDeleteItemVariantModelsResponse> Handle(BulkDeleteItemVariantModelsRequest request, CancellationToken cancellationToken)
    {
        var deleted = await _repository.BulkDeleteAsync(request.Ids, cancellationToken);
        return new BulkDeleteItemVariantModelsResponse { DeletedCount = deleted };
    }
}

internal static class ItemVariantModelValidation
{
    public static async Task ValidateAsync(
        ItemVariantModelUpsertRequestBase request,
        Guid? excludeId,
        IItemVariantModelRepository repository,
        IItemLookupRepository lookupRepository,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsByCodeAsync(request.Code.Trim(), excludeId, cancellationToken))
        {
            throw new InvalidOperationException("Variant model code must be unique within the tenant.");
        }

        _ = await lookupRepository.GetItemTypeByIdAsync(request.ItemTypeId, cancellationToken)
            ?? throw new KeyNotFoundException("Item type not found.");

        var duplicateCodes = request.Attributes
            .GroupBy(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateCodes.Count > 0)
        {
            throw new InvalidOperationException($"Attribute codes must be unique. Duplicates: {string.Join(", ", duplicateCodes)}.");
        }
    }

    public static async Task SaveTemplatesAsync(
        Guid modelId,
        IEnumerable<VariantModelAttributeDefinitionInputDto> attributes,
        IItemVariantModelRepository repository,
        CancellationToken cancellationToken)
    {
        var definitions = attributes.Select(attribute => new AttributeDefinition
        {
            Id = attribute.AttributeDefinitionId ?? Guid.NewGuid(),
            Code = attribute.Code.Trim(),
            Name = attribute.Name.Trim(),
            DataType = attribute.DataType.Trim(),
            IsVariantAxis = attribute.IsVariantAxis,
            IsActive = true
        }).ToList();

        var definitionIdMap = definitions.ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var templates = attributes.Select(attribute => new AttributeTemplate
        {
            VariantModelId = modelId,
            AttributeDefinitionId = definitionIdMap[attribute.Code.Trim()],
            IsRequired = attribute.IsRequired,
            IsVariantAxis = attribute.IsVariantAxis,
            SortOrder = attribute.SortOrder
        }).ToList();

        await repository.ReplaceTemplatesAsync(modelId, definitions, templates, cancellationToken);
    }
}

internal static class ItemVariantModelMapping
{
    public static ItemVariantModelDto ToDto(
        ItemVariantModel entity,
        IReadOnlyList<AttributeTemplate> templates,
        IReadOnlyList<AttributeDefinition> definitions,
        IReadOnlyDictionary<Guid, ItemType> itemTypes)
    {
        itemTypes.TryGetValue(entity.ItemTypeId, out var itemType);
        var definitionMap = definitions.ToDictionary(x => x.Id);

        return new ItemVariantModelDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            ItemTypeId = entity.ItemTypeId,
            ItemType = itemType?.Name ?? string.Empty,
            IsActive = entity.IsActive,
            Attributes = templates
                .OrderBy(x => x.SortOrder)
                .Select(template =>
                {
                    definitionMap.TryGetValue(template.AttributeDefinitionId, out var definition);
                    return new ItemVariantModelAttributeDto
                    {
                        AttributeDefinitionId = template.AttributeDefinitionId,
                        Code = definition?.Code ?? string.Empty,
                        Name = definition?.Name ?? string.Empty,
                        DataType = definition?.DataType ?? string.Empty,
                        IsRequired = template.IsRequired,
                        IsVariantAxis = template.IsVariantAxis,
                        SortOrder = template.SortOrder
                    };
                })
                .ToList()
        };
    }
}
