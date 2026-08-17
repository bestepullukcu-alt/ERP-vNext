using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Queries;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.QueryHandlers;

public sealed class GetFeatureCatalogQueryHandler
    : IRequestHandler<GetFeatureCatalogQuery, Response<PagedResult<FeatureDefinitionListItemDto>>>
{
    private readonly IFeatureDefinitionRepository _repository;
    private readonly IMapper _mapper;

    public GetFeatureCatalogQueryHandler(IFeatureDefinitionRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<PagedResult<FeatureDefinitionListItemDto>>> Handle(GetFeatureCatalogQuery request, CancellationToken ct)
    {
        FeatureDefinitionStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Filter.Status))
        {
            if (!SubscriptionFeatureStatusParser.TryParseFeatureStatus(request.Filter.Status, out var parsedStatus))
            {
                return Response<PagedResult<FeatureDefinitionListItemDto>>.Fail("Status must be Draft, Active, Inactive, Deprecated, or Archived.", 400);
            }

            status = parsedStatus;
        }

        var query = new FeatureDefinitionsQuery(
            request.Filter.Search,
            request.Filter.CategoryId,
            status,
            request.Filter.IsCoreFeature,
            request.Filter.Page,
            request.Filter.PageSize,
            request.Filter.Sort);

        var (items, totalCount) = await _repository.QueryAsync(query, ct);
        var normalizedPageSize = Math.Clamp(request.Filter.PageSize, 1, 200);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        var payload = new PagedResult<FeatureDefinitionListItemDto>(
            _mapper.Map<IReadOnlyList<FeatureDefinitionListItemDto>>(items),
            Math.Max(request.Filter.Page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);

        return Response<PagedResult<FeatureDefinitionListItemDto>>.Success(payload);
    }
}
