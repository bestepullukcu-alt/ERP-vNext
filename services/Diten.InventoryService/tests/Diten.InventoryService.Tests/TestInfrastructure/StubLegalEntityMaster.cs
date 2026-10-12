using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;
using Microsoft.AspNetCore.Http;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// In-process MDM legal-entity master for API tests (W-0173-02). Like MDM, it answers for the CALLER's tenant — read
/// here from the caller's own <c>tenant_id</c> claim, the way MDM reads it from the forwarded token. Legal entities are
/// registered per tenant (active or not); anything unregistered is "not referenceable".
/// </summary>
public sealed class StubLegalEntityMaster : ILegalEntityMasterClient
{
    private readonly ConcurrentDictionary<(Guid Tenant, Guid LegalEntity), bool> _entities = new();
    private readonly ConcurrentDictionary<Guid, bool> _down = new();
    private readonly ConcurrentDictionary<Guid, bool> _refusing = new();
    private readonly ConcurrentDictionary<Guid, int> _calls = new();

    public IHttpContextAccessor? HttpContextAccessor { get; set; }

    public void Register(Guid tenant, Guid legalEntity, bool active = true) => _entities[(tenant, legalEntity)] = active;

    /// <summary>MDM unreachable for this tenant's callers.</summary>
    public void MarkDown(Guid tenant) => _down[tenant] = true;

    /// <summary>MDM answers 403 to this tenant's callers (no <c>mdm.legal-entities.read</c>).</summary>
    public void RefuseCallersOf(Guid tenant) => _refusing[tenant] = true;

    /// <summary>How many times MDM was asked about this legal entity.</summary>
    public int CallsFor(Guid legalEntity) => _calls.GetValueOrDefault(legalEntity);

    public Task<LegalEntityMembership> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        _calls.AddOrUpdate(legalEntityId, 1, (_, n) => n + 1);
        var claim = HttpContextAccessor?.HttpContext?.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(claim, out var tenant))
        {
            return Task.FromResult(LegalEntityMembership.CallerRefused);
        }

        if (_down.ContainsKey(tenant))
        {
            throw new LegalEntityMasterUnavailableException("stub: MDM down");
        }

        if (_refusing.ContainsKey(tenant))
        {
            return Task.FromResult(LegalEntityMembership.CallerRefused);
        }

        return Task.FromResult(_entities.TryGetValue((tenant, legalEntityId), out var active) && active
            ? LegalEntityMembership.Active
            : LegalEntityMembership.NotReferenceable);
    }
}
