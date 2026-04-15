using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Skus.Handlers.CommandHandlers;

internal sealed class UpdateSkuRequestHandler : IRequestHandler<UpdateSkuCommand, bool>
{
    private readonly ISkuRepository _skuRepository;

    public UpdateSkuRequestHandler(ISkuRepository skuRepository)
    {
        _skuRepository = skuRepository;
    }

    public async Task<bool> Handle(UpdateSkuCommand request, CancellationToken cancellationToken)
    {
        var sku = await _skuRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sku == null) return false;

        sku.Code = request.Code;
        sku.ItemId = request.ItemId;
        sku.CompositionId = request.CompositionId;
        sku.CompositionVersion = new CompositionVersion
        {
            VersionNo = request.CompositionVersion
        };
        sku.Packaging = new SkuPackaging
        {
            Form = request.PackagingForm,
            Quantity = request.PackagingQuantity
        };
        sku.Barcode = request.Barcode;
        sku.LifecycleStateId = request.LifecycleStateId;

        return await _skuRepository.UpdateAsync(sku, cancellationToken);
    }
}
