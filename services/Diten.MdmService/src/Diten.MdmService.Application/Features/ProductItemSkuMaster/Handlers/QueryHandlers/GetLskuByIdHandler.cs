using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetLskuByIdHandler
    : IRequestHandler<GetLskuByIdQuery, Response<ProductItemSkuMasterModels.LskuDetailDto>>
{
    private readonly ILskuRepository _lskus;
    private readonly IGskuRepository _gskus;

    public GetLskuByIdHandler(ILskuRepository lskus, IGskuRepository gskus)
    {
        _lskus = lskus;
        _gskus = gskus;
    }

    public async Task<Response<ProductItemSkuMasterModels.LskuDetailDto>> Handle(
        GetLskuByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lsku = await _lskus.GetByIdAsync(request.Id, cancellationToken);
        if (lsku is null)
        {
            return Response<ProductItemSkuMasterModels.LskuDetailDto>.Fail("LSKU_NOT_FOUND", 404);
        }

        var gsku = await _gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
        if (gsku is null)
        {
            return Response<ProductItemSkuMasterModels.LskuDetailDto>.Fail(
                "LSKU_PARENT_BINDING_INVARIANT_VIOLATION",
                409);
        }

        return Response<ProductItemSkuMasterModels.LskuDetailDto>.Success(new(
            lsku.Id,
            lsku.CanonicalCode,
            lsku.GskuId,
            gsku.CanonicalCode,
            lsku.MarketCode,
            lsku.LifecycleStatus,
            lsku.Version,
            lsku.CreatedAt,
            lsku.UpdatedAt));
    }
}
