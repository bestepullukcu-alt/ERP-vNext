namespace Diten.SupplyChainService.Domain.Features.Claims;
public sealed record ClaimMutationResult(Guid ClaimId,string ClaimNumber,Guid ShipmentId,string Status,string? ApprovedAmount,bool IdempotentReplay,int StatusCode,string? ErrorCode=null)
{
 public static ClaimMutationResult Error(int status,string code)=>new(Guid.Empty,"",Guid.Empty,"",null,false,status,code);
}
public sealed class ClaimFailureException(int status,string code):Exception(code)
{ public int Status {get;}=status; public string Code {get;}=code; }
