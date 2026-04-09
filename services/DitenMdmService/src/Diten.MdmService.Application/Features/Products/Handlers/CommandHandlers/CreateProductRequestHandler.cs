using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Handlers.CommandHandlers;

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
        // PERFORMANCE: Seed data calls removed from hot path.
        await ProductLogicHelper.ValidateUpsertAsync(request, null, _productRepository, _lookupRepository, cancellationToken);

        var entity = ProductLogicHelper.ToEntity(request, null);
        var created = await _productRepository.CreateAsync(entity, cancellationToken);
        return created.Id;
    }
}
