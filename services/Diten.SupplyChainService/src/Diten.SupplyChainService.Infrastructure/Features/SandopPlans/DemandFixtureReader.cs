using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Infrastructure.Features.SandopPlans;
public sealed record DemandFixture(Guid TenantId,Guid LegalEntityId,string PlanId,string Version,string Checksum,bool Published);
// This is deliberately a test-only exact fixture seam; no DEMAND HTTP endpoint is inferred.
public sealed class DemandFixtureReader(IEnumerable<DemandFixture> fixtures):IDemandFixtureReader
{ private readonly IReadOnlyList<DemandFixture> fixtures=fixtures.ToArray();
 public Task<bool> MatchesAsync(SandopScope scope,string planId,string version,string? checksum,CancellationToken ct)
 { scope.EnsureTrusted();ct.ThrowIfCancellationRequested();return Task.FromResult(fixtures.Any(x=>x.TenantId==scope.TenantId&&x.LegalEntityId==scope.LegalEntityId&&x.PlanId==planId&&x.Version==version&&x.Published&&(checksum is null||x.Checksum==checksum))); } }
