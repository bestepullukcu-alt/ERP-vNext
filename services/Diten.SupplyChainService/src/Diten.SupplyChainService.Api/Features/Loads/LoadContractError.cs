namespace Diten.SupplyChainService.Api.Features.Loads;
public static class LoadContractError
{ public static object Create(string code,int status,Guid correlation,string? message=null) => new {error=new {code,message=message??(status==503?"Outcome unavailable; retry with the same key.":"Load request could not be completed."),correlationId=correlation},contractVersion="v1"}; }
