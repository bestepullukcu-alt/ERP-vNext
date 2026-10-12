using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Application.Features.Loads;
public sealed class LoadRequestContext
{ public LoadScope Scope {get;set;} = new(Guid.Empty,Guid.Empty,Guid.Empty); public Guid CorrelationId {get;set;} public string IdempotencyKey {get;set;} = ""; public string Authorization {get;set;} = ""; }
