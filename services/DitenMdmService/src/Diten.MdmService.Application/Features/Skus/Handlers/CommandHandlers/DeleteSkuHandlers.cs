using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Skus.Handlers.CommandHandlers;

internal sealed class DeleteSkuRequestHandler : IRequestHandler<DeleteSkuCommand, bool>
{
    private readonly ISkuRepository _skuRepository;

    public DeleteSkuRequestHandler(ISkuRepository skuRepository)
    {
        _skuRepository = skuRepository;
    }

    public async Task<bool> Handle(DeleteSkuCommand request, CancellationToken cancellationToken)
    {
        var sku = await _skuRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sku == null) return false;

        await _skuRepository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}

internal sealed class BulkDeleteSkusRequestHandler : IRequestHandler<BulkDeleteSkusCommand, int>
{
    private readonly ISkuRepository _skuRepository;

    public BulkDeleteSkusRequestHandler(ISkuRepository skuRepository)
    {
        _skuRepository = skuRepository;
    }

    public async Task<int> Handle(BulkDeleteSkusCommand request, CancellationToken cancellationToken)
    {
        return await _skuRepository.BulkDeleteAsync(request.Ids, cancellationToken);
    }
}
