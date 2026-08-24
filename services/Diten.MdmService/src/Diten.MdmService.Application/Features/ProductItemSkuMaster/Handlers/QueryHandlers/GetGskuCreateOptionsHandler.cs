using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGskuCreateOptionsHandler
    : IRequestHandler<GetGskuCreateOptionsQuery, Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>>
{
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IVerifiedGskuReferenceResolver _resolver;

    public GetGskuCreateOptionsHandler(
        IGlobalProductRepository globalProducts,
        IVerifiedGskuReferenceResolver resolver)
    {
        _globalProducts = globalProducts;
        _resolver = resolver;
    }

    public Task<Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>> Handle(
        GetGskuCreateOptionsQuery request,
        CancellationToken cancellationToken) =>
        GskuCreateOptionsFacade.GetAsync(
            _globalProducts,
            _resolver,
            request.PageNumber,
            request.PageSize,
            request.Search,
            cancellationToken);
}
