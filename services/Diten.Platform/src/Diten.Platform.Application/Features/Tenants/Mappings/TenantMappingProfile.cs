using AutoMapper;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.Tenants.Mappings;

public sealed class TenantMappingProfile : Profile
{
    public TenantMappingProfile()
    {
        CreateMap<Tenant, TenantListItemDto>()
            .ForCtorParam("Region", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Region) ? "US" : src.Region))
            .ForCtorParam("Environment", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Environment) ? "Production" : src.Environment))
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam("TenantType", opt => opt.MapFrom(src => src.TenantType.ToString()))
            .ForCtorParam("ProvisioningStatus", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.ProvisioningStatus) ? "Queued" : src.ProvisioningStatus))
            .ForCtorParam("CreatedBy", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.CreatedBy) ? "system" : src.CreatedBy));

        CreateMap<TenantProvisioningStep, TenantProvisioningStepDto>();
        CreateMap<TenantActivityEvent, TenantActivityEventDto>();

        CreateMap<Tenant, TenantOverviewMetricsDto>()
            .ForCtorParam("ProvisioningStepCount", opt => opt.MapFrom(src => src.ProvisioningSteps.Count))
            .ForCtorParam("CompletedProvisioningStepCount", opt => opt.MapFrom(src =>
                src.ProvisioningSteps.Count(step => string.Equals(step.Status, "Completed", StringComparison.OrdinalIgnoreCase))))
            .ForCtorParam("RecentActivityCount", opt => opt.MapFrom(src => src.ActivityTimeline.Count))
            .ForCtorParam("LifecycleStatus", opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam("IsOpenAppAvailable", opt => opt.MapFrom(src => !string.IsNullOrWhiteSpace(src.AppUrl)));

        CreateMap<Tenant, TenantDetailDto>()
            .ForCtorParam("Region", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Region) ? "US" : src.Region))
            .ForCtorParam("Environment", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Environment) ? "Production" : src.Environment))
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam("TenantType", opt => opt.MapFrom(src => src.TenantType.ToString()))
            .ForCtorParam("ProvisioningStatus", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.ProvisioningStatus) ? "Queued" : src.ProvisioningStatus))
            .ForCtorParam("Tier", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Tier) ? "Standard" : src.Tier))
            .ForCtorParam("CreatedBy", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.CreatedBy) ? "system" : src.CreatedBy))
            .ForCtorParam("Overview", opt => opt.MapFrom(src => src))
            .ForCtorParam("ProvisioningSteps", opt => opt.MapFrom(src => src.ProvisioningSteps.OrderBy(step => step.CreatedAt)))
            .ForCtorParam("RecentActivity", opt => opt.MapFrom(src => src.ActivityTimeline.OrderByDescending(activity => activity.At).Take(20)));
    }
}
