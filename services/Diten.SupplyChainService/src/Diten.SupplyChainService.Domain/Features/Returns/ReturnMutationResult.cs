namespace Diten.SupplyChainService.Domain.Features.Returns;
public sealed record ReturnMutationResult(Guid ReturnId, string RmaNumber, Guid ShipmentId, string Status, bool IdempotentReplay, int StatusCode, string? ErrorCode = null, IReadOnlyList<ReturnLine>? Lines = null, string? DetailsJson = null)
{
 public static ReturnMutationResult Error(int status,string code) => new(Guid.Empty,"",Guid.Empty,"",false,status,code);
 public static ReturnMutationResult Error(int status,string code,string? detailsJson) => new(Guid.Empty,"",Guid.Empty,"",false,status,code,null,detailsJson);
}
public sealed class ReturnFailureException(int status,string code) : Exception(code)
{ public int Status { get; } = status; public string Code { get; } = code; }
