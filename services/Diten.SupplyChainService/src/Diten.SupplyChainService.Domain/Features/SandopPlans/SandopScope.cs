namespace Diten.SupplyChainService.Domain.Features.SandopPlans;
public readonly record struct SandopScope(Guid TenantId,Guid LegalEntityId,Guid ActorId)
{ public void EnsureTrusted() { if(TenantId==Guid.Empty||LegalEntityId==Guid.Empty||ActorId==Guid.Empty) throw new InvalidOperationException("Untrusted S&OP scope"); } }
public enum SandopAction { Create, Get, Capture, ListSnapshots, SignOff, ListSignOffs }
public sealed record SandopResult(int Status,string? Code,string? Body)
{ public static SandopResult Error(int status,string code)=>new(status,code,null); }
