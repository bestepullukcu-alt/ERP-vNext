namespace Diten.SupplyChainService.Application.Common;
public sealed record Response<T>(T? Data, int StatusCode, string? ErrorCode = null, string? Message = null, System.Text.Json.Nodes.JsonObject? Details = null) : IResponse<Response<T>>
{
    // BL-501: CentralAuditBehavior files a central record only when this is true — without it a 4xx/5xx envelope
    // (answered, not thrown) would count as a success. Not on the wire: controllers write Data or a ContractError.
    public bool IsSuccessful => StatusCode is >= 200 and < 300;
    public static Response<T> Fail(string code, int status, string? message = null) => new(default, status, code, message ?? code);
    // 3.4.0 (MOD-0183 §23): an error with the contract Error's `details` (line number, inventory code, attempt).
    public static Response<T> FailWith(string code, int status, System.Text.Json.Nodes.JsonObject? details) => new(default, status, code, code, details);
    public static Response<T> Success(T data, int status = 200) => new(data, status);
}
