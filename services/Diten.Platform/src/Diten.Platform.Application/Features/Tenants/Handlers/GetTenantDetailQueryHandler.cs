using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantDetailQueryHandler : IRequestHandler<GetTenantDetailQuery, TenantDetailDto?>
{
    private readonly ITenantRegistryRepository _repository;

    public GetTenantDetailQueryHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<TenantDetailDto?> Handle(GetTenantDetailQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var provisioningSteps = tenant.ProvisioningSteps
            .OrderBy(x => x.CreatedAt)
            .Select(x => new TenantProvisioningStepDto(x.Key, x.Label, x.Status, x.CreatedAt, x.CompletedAt, x.Detail))
            .ToList();

        var recentActivity = tenant.ActivityTimeline
            .OrderByDescending(x => x.At)
            .Take(20)
            .Select(x => new TenantActivityEventDto(x.EventType, x.Message, x.At, x.Actor))
            .ToList();

        var overview = new TenantOverviewMetricsDto(
            provisioningSteps.Count,
            provisioningSteps.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)),
            recentActivity.Count,
            tenant.Status.ToString(),
            !string.IsNullOrWhiteSpace(tenant.AppUrl));

        return new TenantDetailDto(
            tenant.Id,
            tenant.Code,
            tenant.Name,
            string.IsNullOrWhiteSpace(tenant.DisplayName) ? tenant.Name : tenant.DisplayName,
            tenant.Domain,
            tenant.Region ?? "US",
            tenant.Environment ?? "Production",
            tenant.Status.ToString(),
            string.IsNullOrWhiteSpace(tenant.ProvisioningStatus) ? "Queued" : tenant.ProvisioningStatus,
            tenant.Tier ?? "Standard",
            tenant.AppUrl,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            string.IsNullOrWhiteSpace(tenant.CreatedBy) ? "system" : tenant.CreatedBy,
            overview,
            provisioningSteps,
            recentActivity);
    }
}
