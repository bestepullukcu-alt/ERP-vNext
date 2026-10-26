using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// Degraded — never Unhealthy: commands keep running whatever the audit delivery does — when delivery is not configured,
/// when a waiting row is older than <see cref="CentralAuditOptions.StaleAfter"/>, or when Platform refused rows. The data
/// carries the counts, never the key. Exposed by the service with <c>app.MapHealthChecks("/health/central-audit")</c>.
/// </summary>
public sealed class CentralAuditHealthCheck : IHealthCheck
{
    public const string Name = "central-audit";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<CentralAuditOptions> _options;
    private readonly TimeProvider _clock;

    public CentralAuditHealthCheck(IServiceScopeFactory scopes, IOptionsMonitor<CentralAuditOptions> options, TimeProvider clock)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        using var scope = _scopes.CreateScope();
        var backlog = await scope.ServiceProvider.GetRequiredService<ICentralAuditOutboxStore>().GetBacklogAsync(cancellationToken);
        var oldestAge = backlog.OldestPendingCreatedAtUtc is { } oldest ? _clock.GetUtcNow().UtcDateTime - oldest : TimeSpan.Zero;

        var data = new Dictionary<string, object>
        {
            ["configured"] = options.IsConfigured,
            ["pending"] = backlog.Pending,
            ["rejected"] = backlog.Rejected,
            ["oldestPendingSeconds"] = (long)oldestAge.TotalSeconds
        };

        var problems = new List<string>();
        if (!options.IsConfigured)
        {
            problems.Add($"{CentralAuditOptions.PlatformSection}:BaseUrl / InternalApiKey not configured — events accumulate, none reach Platform");
        }

        if (backlog.Pending > 0 && oldestAge > options.StaleAfter)
        {
            problems.Add($"oldest waiting event is {(long)oldestAge.TotalMinutes} min old");
        }

        if (backlog.Rejected > 0)
        {
            problems.Add($"{backlog.Rejected} event(s) refused by Platform");
        }

        return problems.Count == 0
            ? HealthCheckResult.Healthy("Central audit delivery is working.", data)
            : HealthCheckResult.Degraded(string.Join("; ", problems), data: data);
    }
}
