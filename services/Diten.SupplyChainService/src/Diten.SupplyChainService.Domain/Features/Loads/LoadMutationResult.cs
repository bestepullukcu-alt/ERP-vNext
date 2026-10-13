namespace Diten.SupplyChainService.Domain.Features.Loads;
public sealed record LoadMutationResult(Guid LoadId, string LoadNumber, string Status, bool IdempotentReplay, int StatusCode, string? ErrorCode = null)
{ public static LoadMutationResult Error(int status,string code) => new(Guid.Empty,"","",false,status,code); }
public sealed class LoadFailureException(int status, string code) : Exception(code)
{ public int Status { get; } = status; public string Code { get; } = code; }
