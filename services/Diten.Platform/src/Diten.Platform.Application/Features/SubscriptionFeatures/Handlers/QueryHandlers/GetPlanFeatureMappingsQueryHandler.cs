using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.QueryHandlers;

public sealed class GetPlanFeatureMappingsQueryHandler
    : IRequestHandler<GetPlanFeatureMappingsQuery, Response<IReadOnlyList<PlanFeatureMappingDto>>>
{
    private readonly IPlanFeatureMappingRepository _mappingRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IFeatureDefinitionRepository _featureRepository;

    public GetPlanFeatureMappingsQueryHandler(
        IPlanFeatureMappingRepository mappingRepository,
        ISubscriptionPlanRepository planRepository,
        IFeatureDefinitionRepository featureRepository)
    {
        _mappingRepository = mappingRepository;
        _planRepository = planRepository;
        _featureRepository = featureRepository;
    }

    public async Task<Response<IReadOnlyList<PlanFeatureMappingDto>>> Handle(GetPlanFeatureMappingsQuery request, CancellationToken ct)
    {
        var plan = await _planRepository.GetByIdAsync(request.SubscriptionPlanId, ct);
        if (plan is null)
        {
            return Response<IReadOnlyList<PlanFeatureMappingDto>>.Fail("Subscription plan not found.", 404);
        }

        var mappings = await _mappingRepository.GetByPlanIdAsync(request.SubscriptionPlanId, ct);
        var dtos = new List<PlanFeatureMappingDto>(mappings.Count);
        foreach (var mapping in mappings)
        {
            var feature = await _featureRepository.GetByIdAsync(mapping.FeatureDefinitionId, ct);
            if (feature is null)
            {
                continue;
            }

            dtos.Add(new PlanFeatureMappingDto(
                mapping.Id,
                mapping.SubscriptionPlanId,
                plan.Code,
                plan.Name,
                mapping.FeatureDefinitionId,
                feature.FeatureCode,
                feature.DisplayName,
                mapping.AvailabilityStatus.ToString(),
                mapping.EffectiveFromUtc,
                mapping.EffectiveToUtc,
                mapping.RowVersion,
                mapping.CreatedAt,
                mapping.UpdatedAt));
        }

        return Response<IReadOnlyList<PlanFeatureMappingDto>>.Success(dtos);
    }
}
