using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns;
public sealed class ReturnRequestContext
{ public ReturnScope Scope {get;set;} = new(Guid.Empty,Guid.Empty,Guid.Empty); public Guid CorrelationId {get;set;} public string IdempotencyKey {get;set;} = ""; public string Authorization {get;set;} = ""; }
