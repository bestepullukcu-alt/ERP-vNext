using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Skus.Handlers.CommandHandlers;

internal sealed class CreateSkuRequestHandler : IRequestHandler<CreateSkuCommand, Guid>
{
    private readonly ISkuRepository _skuRepository;

    public CreateSkuRequestHandler(ISkuRepository skuRepository)
    {
        _skuRepository = skuRepository;
    }

    public async Task<Guid> Handle(CreateSkuCommand request, CancellationToken cancellationToken)
    {
        var sku = new Sku
        {
            Code = request.Code,
            ItemId = request.ItemId,
            CompositionId = request.CompositionId,
            CompositionVersion = new CompositionVersion
            {
                VersionNo = request.CompositionVersion
            },
            Packaging = new SkuPackaging
            {
                Form = request.PackagingForm,
                Quantity = request.PackagingQuantity
            },
            Barcode = request.Barcode,
            LifecycleStateId = request.LifecycleStateId
        };

        await _skuRepository.CreateAsync(sku, cancellationToken);
        return sku.Id;
    }
}
