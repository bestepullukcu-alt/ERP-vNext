using AutoMapper;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Mappings;

public sealed class SubscriptionPlanMappingProfile : Profile
{
    public SubscriptionPlanMappingProfile()
    {
        CreateMap<SubscriptionPlan, SubscriptionPlanDto>();
        CreateMap<SubscriptionPlan, SubscriptionPlanListItemDto>();

        CreateMap<SubscriptionPlanSummary, SubscriptionPlanSummaryDto>()
            .ForCtorParam("TotalPlans", opt => opt.MapFrom(src => src.Total))
            .ForCtorParam("ActivePlans", opt => opt.MapFrom(src => src.Active));
    }
}
