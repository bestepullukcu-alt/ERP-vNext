using Diten.Application.EnterpriseStrategy.Adapters.Ppm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Diten.WebAPI.Health;

public sealed class DeliveryExecutionManagementDependencyHealthCheck : IHealthCheck
{
    private readonly IPpmInitiativeReadAdapter _initiativeAdapter;

    public DeliveryExecutionManagementDependencyHealthCheck(IPpmInitiativeReadAdapter initiativeAdapter)
    {
        _initiativeAdapter = initiativeAdapter;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await _initiativeAdapter.ListAsync(1, 1, linked.Token);
            return HealthCheckResult.Healthy("PPM adapter reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("PPM adapter degraded; cached/fallback mode expected.", ex);
        }
    }
}
