using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Handlers.CommandHandlers;

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
        // PERFORMANCE: Seed data calls removed from hot path.
        var existing = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        await ProductLogicHelper.ValidateUpsertAsync(request, request.Id, _productRepository, _lookupRepository, cancellationToken);

        var entity = ProductLogicHelper.ToEntity(request, existing);
        return await _productRepository.UpdateAsync(entity, cancellationToken);
    }
}
