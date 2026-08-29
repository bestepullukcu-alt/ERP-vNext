using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.QueryHandlers;

public sealed class GetBrandListHandler : IRequestHandler<Queries.GetBrandListQuery, Response<BrandListResultDto>>
{
    private readonly IBrandRepository _repository;

    public GetBrandListHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BrandListResultDto>> Handle(Queries.GetBrandListQuery request, CancellationToken cancellationToken)
    {
        // The repository is already tenant-scoped, so cross-tenant rows can never enter this pipeline.
        var brands = await _repository.GetAllAsync(cancellationToken);
        IEnumerable<Domain.Entities.Brand> filtered = brands;

        if (!request.IncludeArchived)
        {
            filtered = filtered.Where(x => !x.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(x =>
                x.BrandCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.BrandName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.BrandStatus))
        {
            var status = request.BrandStatus.Trim();
            filtered = filtered.Where(x => string.Equals(x.BrandStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (request.BusinessUnitId is { } businessUnitId && businessUnitId != Guid.Empty)
        {
            filtered = filtered.Where(x => x.BusinessUnitId == businessUnitId);
        }

        if (request.TherapeuticAreaId is { } therapeuticAreaId && therapeuticAreaId != Guid.Empty)
        {
            filtered = filtered.Where(x => x.TherapeuticAreaId == therapeuticAreaId);
        }

        var items = filtered
            .OrderBy(x => x.BrandName, StringComparer.OrdinalIgnoreCase)
            .Select(BrandMappings.ToDetailDto)
            .ToList();

        return Response<BrandListResultDto>.Success(new BrandListResultDto(items, items.Count));
    }
}
