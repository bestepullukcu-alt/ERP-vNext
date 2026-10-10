namespace Diten.SupplyChainService.Domain.Features.Returns;
public sealed record ReturnMutationResult(Guid ReturnId, string RmaNumber, Guid ShipmentId, string Status, bool IdempotentReplay, int StatusCode, string? ErrorCode = null)
{ public static ReturnMutationResult Error(int status,string code) => new(Guid.Empty,"",Guid.Empty,"",false,status,code); }
public sealed class ReturnFailureException(int status,string code) : Exception(code)
{ public int Status { get; } = status; public string Code { get; } = code; }
