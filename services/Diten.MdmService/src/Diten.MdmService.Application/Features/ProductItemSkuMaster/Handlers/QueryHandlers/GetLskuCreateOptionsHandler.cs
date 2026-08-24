using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetLskuCreateOptionsHandler
    : IRequestHandler<GetLskuCreateOptionsQuery, Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>>
{
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IVerifiedMarketReferenceResolver _markets;

    public GetLskuCreateOptionsHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IVerifiedMarketReferenceResolver markets)
    {
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _markets = markets;
    }

    public Task<Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>> Handle(
        GetLskuCreateOptionsQuery request,
        CancellationToken cancellationToken) =>
        LskuCreateOptionsFacade.GetAsync(
            _gskus,
            _revisions,
            _globalProducts,
            _markets,
            request.PageNumber,
            request.PageSize,
            request.Search,
            cancellationToken);
}
