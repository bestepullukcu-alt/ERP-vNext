using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Handlers.CommandHandlers;

public sealed class DeleteProductRequestHandler : IRequestHandler<DeleteProductRequest, bool>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public DeleteProductRequestHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<bool> Handle(DeleteProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        await ProductLogicHelper.ValidateDeleteAsync(product, _lookupRepository, cancellationToken);

        await _productRepository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}
