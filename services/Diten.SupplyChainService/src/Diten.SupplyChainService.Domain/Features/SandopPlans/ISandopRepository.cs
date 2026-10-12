using System.Text.Json;
namespace Diten.SupplyChainService.Domain.Features.SandopPlans;
public interface IDemandFixtureReader
{ Task<bool> MatchesAsync(SandopScope scope,string planId,string version,string? checksum,CancellationToken ct); }
public interface ISandopRepository
{ Task<SandopResult> MutateAsync(SandopScope scope,SandopAction action,Guid? planId,string key,string fingerprint,Guid correlation,JsonElement body,IDemandFixtureReader fixture,CancellationToken ct);
Task<SandopResult> ReadAsync(SandopScope scope,SandopAction action,Guid planId,CancellationToken ct); }
