using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Tests;

// Shared test double for FU13 version-bump assertions: records every IncrementAsync call per tenant.
internal sealed class FakeRoleAssignmentVersionService : IRoleAssignmentVersionService
{
    private readonly Dictionary<Guid, long> _versions = new();

    public List<Guid> IncrementedTenants { get; } = [];

    public int IncrementCount => IncrementedTenants.Count;

    public Task<long> GetAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult(_versions.TryGetValue(tenantId, out var v) ? v : 0);

    public Task<long> IncrementAsync(Guid tenantId, CancellationToken ct)
    {
        var next = (_versions.TryGetValue(tenantId, out var v) ? v : 0) + 1;
        _versions[tenantId] = next;
        IncrementedTenants.Add(tenantId);
        return Task.FromResult(next);
    }
}
