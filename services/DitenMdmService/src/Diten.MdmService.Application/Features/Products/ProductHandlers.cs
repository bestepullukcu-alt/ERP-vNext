using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Products;

public sealed class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, IReadOnlyList<ProductListItemDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetAllProductsQueryHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var products = await _productRepository.GetAllAsync(cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);
        var lifecycleMap = lifecycleStates.ToDictionary(x => x.Id);

        return products
            .Select(product =>
            {
                var category = ProductCatalog.GetCategoryDefinition(product.CategoryId);
                var lifecycleState = lifecycleMap[product.LifecycleStateId];
                return ProductMapping.ToListDto(product, category, lifecycleState);
            })
            .ToList();
    }
}

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto?>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<ProductDetailDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var category = ProductCatalog.GetCategoryDefinition(product.CategoryId);
        var lifecycleState = await _lookupRepository.GetLifecycleStateByIdAsync(product.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Lifecycle state not found.");

        return ProductMapping.ToDetailDto(product, category, lifecycleState);
    }
}

public sealed class CreateProductRequestHandler : IRequestHandler<CreateProductRequest, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public CreateProductRequestHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        await ProductValidation.ValidateUpsertAsync(request, null, _productRepository, _lookupRepository, cancellationToken);

        var entity = ProductValidation.ToEntity(request, null);
        var created = await _productRepository.CreateAsync(entity, cancellationToken);
        return created.Id;
    }
}

public sealed class UpdateProductRequestHandler : IRequestHandler<UpdateProductRequest, bool>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public UpdateProductRequestHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<bool> Handle(UpdateProductRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var existing = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        await ProductValidation.ValidateUpsertAsync(request, request.Id, _productRepository, _lookupRepository, cancellationToken);

        var entity = ProductValidation.ToEntity(request, existing);
        return await _productRepository.UpdateAsync(entity, cancellationToken);
    }
}

public sealed class ChangeProductLifecycleRequestHandler : IRequestHandler<ChangeProductLifecycleRequest, bool>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public ChangeProductLifecycleRequestHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<bool> Handle(ChangeProductLifecycleRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var existing = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var currentState = await _lookupRepository.GetLifecycleStateByIdAsync(existing.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Current lifecycle state not found.");
        var targetState = await _lookupRepository.GetLifecycleStateByIdAsync(request.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Target lifecycle state not found.");

        ProductValidation.ValidateLifecycleTransition(currentState.Code, targetState.Code);

        existing.LifecycleStateId = request.LifecycleStateId;
        return await _productRepository.UpdateAsync(existing, cancellationToken);
    }
}

public sealed class DeleteProductRequestHandler : IRequestHandler<DeleteProductRequest, bool>
{
    private readonly IProductRepository _productRepository;

    public DeleteProductRequestHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> Handle(DeleteProductRequest request, CancellationToken cancellationToken)
    {
        await _productRepository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}

public sealed class BulkDeleteProductsRequestHandler : IRequestHandler<BulkDeleteProductsRequest, BulkDeleteProductsResponse>
{
    private readonly IProductRepository _productRepository;

    public BulkDeleteProductsRequestHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<BulkDeleteProductsResponse> Handle(BulkDeleteProductsRequest request, CancellationToken cancellationToken)
    {
        var deletedCount = await _productRepository.BulkDeleteAsync(request.Ids, cancellationToken);
        return new BulkDeleteProductsResponse { DeletedCount = deletedCount };
    }
}

internal static class ProductValidation
{
    public static async Task ValidateUpsertAsync(
        ProductUpsertRequestBase request,
        Guid? excludeId,
        IProductRepository productRepository,
        IItemLookupRepository lookupRepository,
        CancellationToken cancellationToken)
    {
        if (await productRepository.ExistsByCodeAsync(request.Code.Trim(), excludeId, cancellationToken))
        {
            throw new InvalidOperationException("Product code must be unique within the tenant.");
        }

        if (!Enum.IsDefined(request.ProductType))
        {
            throw new InvalidOperationException("Product type is invalid.");
        }

        if (!ProductCatalog.IsCategoryValidForProductType(request.CategoryId, request.ProductType))
        {
            throw new InvalidOperationException("Selected category must belong to the selected product type.");
        }

        _ = await lookupRepository.GetLifecycleStateByIdAsync(request.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Lifecycle state not found.");
    }

    public static Product ToEntity(ProductUpsertRequestBase request, Product? existing)
    {
        var entity = existing ?? new Product();
        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.ShortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.ProductType = request.ProductType;
        entity.CategoryId = request.CategoryId;
        entity.LifecycleStateId = request.LifecycleStateId;
        entity.IsSaleable = request.IsSaleable;
        entity.IsPurchasable = request.IsPurchasable;
        entity.IsManufacturable = request.IsManufacturable;
        return entity;
    }

    public static void ValidateLifecycleTransition(string currentCode, string targetCode)
    {
        var normalizedCurrent = (currentCode ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedTarget = (targetCode ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedCurrent == normalizedTarget)
        {
            return;
        }

        var allowed = normalizedCurrent switch
        {
            "DRAFT" => new[] { "ACTIVE" },
            "ACTIVE" => new[] { "BLOCKED", "OBSOLETE" },
            "BLOCKED" => new[] { "ACTIVE", "OBSOLETE" },
            "OBSOLETE" => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };

        if (!allowed.Contains(normalizedTarget))
        {
            throw new InvalidOperationException("Requested lifecycle transition is not allowed.");
        }
    }
}
