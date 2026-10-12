using Diten.SupplyChainService.Domain.Features.Claims;
namespace Diten.SupplyChainService.Application.Features.Claims;
public sealed class ClaimRequestContext
{
 public ClaimScope Scope {get;set;}=new(Guid.Empty,Guid.Empty,Guid.Empty);
 public Guid CorrelationId {get;set;}
 public string IdempotencyKey {get;set;}="";
 public string Authorization {get;set;}="";
 public HashSet<string> Permissions {get;set;}=new(StringComparer.Ordinal);
}
