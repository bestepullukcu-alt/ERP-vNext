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
            .ForCtorParam("Plan", opt => opt.MapFrom(src => !string.IsNullOrWhiteSpace(src.PlanName) ? src.PlanName : (!string.IsNullOrWhiteSpace(src.PlanCode) ? src.PlanCode : (string.IsNullOrWhiteSpace(src.Tier) ? "Standard" : src.Tier))))
            .ForCtorParam("PlanId", opt => opt.MapFrom(src => src.PlanId))
            .ForCtorParam("PlanCode", opt => opt.MapFrom(src => src.PlanCode))
            .ForCtorParam("PlanName", opt => opt.MapFrom(src => src.PlanName))
            .ForCtorParam("SubscriptionStatus", opt => opt.MapFrom(src => src.SubscriptionStatus.ToString()))
            .ForCtorParam("TrialStartDateUtc", opt => opt.MapFrom(src => src.TrialStartDateUtc))
            .ForCtorParam("TrialEndDateUtc", opt => opt.MapFrom(src => src.TrialEndDateUtc))
            .ForCtorParam("ProvisioningStatus", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.ProvisioningStatus) ? "Queued" : src.ProvisioningStatus))
            .ForCtorParam("StorageUsagePercent", opt => opt.MapFrom(src => src.StorageQuotaGb <= 0 ? 0 : Math.Round(src.StorageUsedGb / src.StorageQuotaGb * 100, 1)))
            .ForCtorParam("IsOverQuota", opt => opt.MapFrom(src => src.IsOverQuota))
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
            .ForCtorParam("Tier", opt => opt.MapFrom(src => !string.IsNullOrWhiteSpace(src.PlanCode) ? src.PlanCode : (string.IsNullOrWhiteSpace(src.Tier) ? "Standard" : src.Tier)))
            .ForCtorParam("PlanId", opt => opt.MapFrom(src => src.PlanId))
            .ForCtorParam("PlanCode", opt => opt.MapFrom(src => src.PlanCode))
            .ForCtorParam("PlanName", opt => opt.MapFrom(src => src.PlanName))
            .ForCtorParam("SubscriptionStatus", opt => opt.MapFrom(src => src.SubscriptionStatus.ToString()))
            .ForCtorParam("TrialStartDateUtc", opt => opt.MapFrom(src => src.TrialStartDateUtc))
            .ForCtorParam("TrialEndDateUtc", opt => opt.MapFrom(src => src.TrialEndDateUtc))
            .ForCtorParam("LogoDataUrl", opt => opt.MapFrom(src => src.LogoDataUrl))
            .ForCtorParam("FaviconDataUrl", opt => opt.MapFrom(src => src.FaviconDataUrl))
            .ForCtorParam("CreatedBy", opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.CreatedBy) ? "system" : src.CreatedBy))
            .ForCtorParam("Overview", opt => opt.MapFrom(src => src))
            .ForCtorParam("ProvisioningSteps", opt => opt.MapFrom(src => src.ProvisioningSteps.OrderBy(step => step.CreatedAt)))
            .ForCtorParam("RecentActivity", opt => opt.MapFrom(src => src.ActivityTimeline.OrderByDescending(activity => activity.At).Take(20)));
    }
}
