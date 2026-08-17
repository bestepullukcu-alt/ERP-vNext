using AutoMapper;
using Diten.Platform.Domain.Features.SubscriptionFeatures;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Mappings;

public sealed class SubscriptionFeatureMappingProfile : Profile
{
    public SubscriptionFeatureMappingProfile()
    {
        CreateMap<FeatureDefinition, FeatureDefinitionDto>()
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<FeatureDefinition, FeatureDefinitionListItemDto>()
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<FeatureCategory, FeatureCategoryDto>()
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<PlanFeatureMapping, PlanFeatureMappingDto>()
            .ConstructUsing(src => new PlanFeatureMappingDto(
                src.Id,
                src.SubscriptionPlanId,
                string.Empty,
                string.Empty,
                src.FeatureDefinitionId,
                string.Empty,
                string.Empty,
                src.AvailabilityStatus.ToString(),
                src.EffectiveFromUtc,
                src.EffectiveToUtc,
                src.RowVersion,
                src.CreatedAt,
                src.UpdatedAt));
    }
}
