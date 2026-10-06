using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.QueryHandlers;

public sealed class GetBrandListHandler : IRequestHandler<Queries.GetBrandListQuery, Response<BrandListResultDto>>
{
    private readonly IBrandRepository _repository;
    private readonly IProductRepository _products;

    public GetBrandListHandler(IBrandRepository repository, IProductRepository products)
    {
        _repository = repository;
        _products = products;
    }

    public async Task<Response<BrandListResultDto>> Handle(Queries.GetBrandListQuery request, CancellationToken cancellationToken)
    {
        if (InvalidPaging(request) is { } invalid)
        {
            return BrandProductFailures.Fail<BrandListResultDto>(BrandProductReasonCodes.ListQueryInvalid, invalid, 400);
        }

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

        if (request.Length is not { } length)
        {
            var items = filtered
                .OrderBy(x => x.BrandName, StringComparer.OrdinalIgnoreCase)
                .Select(BrandMappings.ToDetailDto)
                .ToList();

            return Response<BrandListResultDto>.Success(new BrandListResultDto(items, items.Count));
        }

        // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 6) — the register's page: sorted on the server, then cut on the server.
        var matching = Sort(filtered, request.OrderBy, request.OrderDir).ToList();
        var start = request.Start ?? 0;
        var page = matching.Skip(start).Take(length).Select(BrandMappings.ToDetailDto).ToList();
        return Response<BrandListResultDto>.Success(new BrandListResultDto(
            page, matching.Count, start, length, await ArchivedWithLiveProductsAsync(brands, cancellationToken)));
    }

    private static string? InvalidPaging(Queries.GetBrandListQuery request)
    {
        if (request.Length is { } length && (length < 1 || length > Queries.GetBrandListQuery.MaxLength))
        {
            return $"length must be between 1 and {Queries.GetBrandListQuery.MaxLength}.";
        }

        if (request.Start is < 0)
        {
            return "start must not be negative.";
        }

        if (request.OrderBy is { Length: > 0 } orderBy
            && !Queries.GetBrandListQuery.SortableFields.Contains(orderBy, StringComparer.Ordinal))
        {
            return $"orderBy '{orderBy}' is not a sortable brand field.";
        }

        return request.OrderDir is { Length: > 0 } orderDir && orderDir is not ("asc" or "desc")
            ? $"orderDir '{orderDir}' must be asc or desc."
            : null;
    }

    private static IEnumerable<Domain.Entities.Brand> Sort(
        IEnumerable<Domain.Entities.Brand> brands, string? orderBy, string? orderDir)
    {
        var descending = orderDir == "desc";
        IOrderedEnumerable<Domain.Entities.Brand> sorted = orderBy switch
        {
            "brandCode" => By(x => x.BrandCode, StringComparer.OrdinalIgnoreCase),
            "brandStatus" => By(x => x.BrandStatus, StringComparer.OrdinalIgnoreCase),
            "effectiveFrom" => By(x => x.EffectiveFrom, Comparer<DateTimeOffset>.Default),
            "effectiveTo" => By(x => x.EffectiveTo, Comparer<DateTimeOffset?>.Default),
            "isArchived" => By(x => x.IsArchived, Comparer<bool>.Default),
            "updatedAt" => By(x => x.UpdatedAt, Comparer<DateTimeOffset?>.Default),
            _ => By(x => x.BrandName, StringComparer.OrdinalIgnoreCase)
        };
        // A stable tiebreak, so the same request always returns the same page.
        return sorted.ThenBy(x => x.BrandCode, StringComparer.Ordinal).ThenBy(x => x.Id);

        IOrderedEnumerable<Domain.Entities.Brand> By<TKey>(Func<Domain.Entities.Brand, TKey> key, IComparer<TKey> comparer)
            => descending ? brands.OrderByDescending(key, comparer) : brands.OrderBy(key, comparer);
    }

    /// <summary>FIX1 (item 3) — archived brands that still have at least one product that is not archived.</summary>
    private async Task<int> ArchivedWithLiveProductsAsync(
        IReadOnlyList<Domain.Entities.Brand> brands, CancellationToken cancellationToken)
    {
        var archived = brands.Where(x => x.IsArchived).Select(x => x.Id).ToHashSet();
        if (archived.Count == 0)
        {
            return 0;
        }

        return (await _products.GetAllAsync(cancellationToken))
            .Where(x => !x.IsArchived && x.BrandId is { } brandId && archived.Contains(brandId))
            .Select(x => x.BrandId)
            .Distinct()
            .Count();
    }
}
