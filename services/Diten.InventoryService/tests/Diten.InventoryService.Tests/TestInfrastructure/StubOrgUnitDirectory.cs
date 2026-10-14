using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// In-process Platform organization-unit directory for location API tests: a unit is valid only for the legal entity
/// it was registered under. <see cref="Calls"/> counts lookups (an unchanged orgUnitRef must not call Platform).
/// The unreachable path is measured against the REAL HTTP client (<see cref="RealOrgUnitClientApiFactory"/>).
/// </summary>
public sealed class StubOrgUnitDirectory : IOrgUnitDirectoryClient
{
    private readonly ConcurrentDictionary<Guid, Guid> _units = new();
    private int _calls;

    public int Calls => _calls;

    public Guid AddUnit(Guid legalEntityId)
    {
        var id = Guid.NewGuid();
        _units[id] = legalEntityId;
        return id;
    }

    public Task<OrgUnitCheck> CheckAsync(Guid organizationUnitId, Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(_units.TryGetValue(organizationUnitId, out var le) && le == legalEntityId ? OrgUnitCheck.Valid : OrgUnitCheck.Invalid);
    }
}
