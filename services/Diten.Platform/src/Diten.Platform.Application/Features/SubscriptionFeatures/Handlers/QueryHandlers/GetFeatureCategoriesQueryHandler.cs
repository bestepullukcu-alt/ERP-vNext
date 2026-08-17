using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Queries;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.QueryHandlers;

public sealed class GetFeatureCategoriesQueryHandler : IRequestHandler<GetFeatureCategoriesQuery, Response<IReadOnlyList<FeatureCategoryDto>>>
{
    private readonly IFeatureCategoryRepository _repository;
    private readonly IMapper _mapper;

    public GetFeatureCategoriesQueryHandler(IFeatureCategoryRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<IReadOnlyList<FeatureCategoryDto>>> Handle(GetFeatureCategoriesQuery request, CancellationToken ct)
    {
        FeatureCategoryStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!SubscriptionFeatureStatusParser.TryParseCategoryStatus(request.Status, out var parsedStatus))
            {
                return Response<IReadOnlyList<FeatureCategoryDto>>.Fail("Status must be Active, Inactive, or Archived.", 400);
            }

            status = parsedStatus;
        }

        var categories = await _repository.GetAllAsync(status, ct);
        return Response<IReadOnlyList<FeatureCategoryDto>>.Success(_mapper.Map<IReadOnlyList<FeatureCategoryDto>>(categories));
    }
}
