using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.QueryHandlers;

public sealed class GetBrandByIdHandler : IRequestHandler<Queries.GetBrandByIdQuery, Response<BrandDetailDto>>
{
    private readonly IBrandRepository _repository;
    private readonly IProductRepository _products;

    public GetBrandByIdHandler(IBrandRepository repository, IProductRepository products)
    {
        _repository = repository;
        _products = products;
    }

    public async Task<Response<BrandDetailDto>> Handle(Queries.GetBrandByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BrandId, cancellationToken);

        // Archived brands stay readable on purpose — archiving closes writes, not history.
        if (entity is null)
        {
            return BrandProductFailures.Fail<BrandDetailDto>(BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
        }

        // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 3) — how many of its products are still in use: the archive confirmation says
        // it before an irreversible archive, and the detail page keeps saying it after one.
        var liveProducts = (await _products.GetByBrandAsync(entity.Id, cancellationToken)).Count(x => !x.IsArchived);
        return Response<BrandDetailDto>.Success(BrandMappings.ToDetailDto(entity) with { LiveProductCount = liveProducts });
    }
}
