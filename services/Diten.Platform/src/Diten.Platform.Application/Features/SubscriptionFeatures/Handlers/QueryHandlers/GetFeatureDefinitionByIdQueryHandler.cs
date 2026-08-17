using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.QueryHandlers;

public sealed class GetFeatureDefinitionByIdQueryHandler : IRequestHandler<GetFeatureDefinitionByIdQuery, Response<FeatureDefinitionDto>>
{
    private readonly IFeatureDefinitionRepository _repository;
    private readonly IMapper _mapper;

    public GetFeatureDefinitionByIdQueryHandler(IFeatureDefinitionRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<FeatureDefinitionDto>> Handle(GetFeatureDefinitionByIdQuery request, CancellationToken ct)
    {
        var feature = await _repository.GetByIdAsync(request.Id, ct);
        if (feature is null)
        {
            return Response<FeatureDefinitionDto>.Fail("Subscription feature not found.", 404);
        }

        return Response<FeatureDefinitionDto>.Success(_mapper.Map<FeatureDefinitionDto>(feature));
    }
}
