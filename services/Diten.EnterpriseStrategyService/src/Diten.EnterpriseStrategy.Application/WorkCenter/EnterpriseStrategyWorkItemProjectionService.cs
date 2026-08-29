namespace Diten.Application.WorkCenter;

public sealed class EnterpriseStrategyWorkItemProjectionService
{
    private readonly bool _enabled;

    public EnterpriseStrategyWorkItemProjectionService(bool enabled)
    {
        _enabled = enabled;
    }

    public bool Enabled => _enabled;

    public Task<EnterpriseStrategyWorkItemEnvelope> GetAsync(
        string? scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Both disabled and explicitly enabled preparation modes remain fail-closed. There is deliberately no
        // repository dependency: existing ES records cannot be read tenant-safely until a migration is approved.
        _ = scope;
        return Task.FromResult(EnterpriseStrategyWorkItemEnvelope.TenantSafetyUnavailable());
    }
}
