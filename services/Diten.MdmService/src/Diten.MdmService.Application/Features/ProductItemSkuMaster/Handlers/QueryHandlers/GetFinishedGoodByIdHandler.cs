using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetFinishedGoodByIdHandler
    : IRequestHandler<GetFinishedGoodByIdQuery, Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>>
{
    private readonly IFinishedGoodRepository _finishedGoods;
    private readonly IGskuRepository _gskus;

    public GetFinishedGoodByIdHandler(IFinishedGoodRepository finishedGoods, IGskuRepository gskus)
    {
        _finishedGoods = finishedGoods;
        _gskus = gskus;
    }

    public async Task<Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>> Handle(
        GetFinishedGoodByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var finishedGood = await _finishedGoods.GetByIdAsync(request.Id, cancellationToken);
        if (finishedGood is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Fail("FINISHED_GOOD_NOT_FOUND", 404);
        }

        var gsku = await _gskus.GetByIdAsync(finishedGood.GskuId, cancellationToken);
        if (gsku is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Fail(
                "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                500);
        }

        return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Success(new(
            finishedGood.Id,
            finishedGood.CanonicalCode,
            finishedGood.GskuId,
            gsku.CanonicalCode,
            gsku.CanonicalCode,
            finishedGood.LifecycleStatus,
            finishedGood.Version,
            finishedGood.CreatedAt,
            finishedGood.UpdatedAt));
    }
}
