namespace Diten.SupplyChainService.Application.Common;
public sealed record Response<T>(T? Data, int StatusCode, string? ErrorCode = null, string? Message = null) : IResponse<Response<T>>
{
    public static Response<T> Fail(string code, int status, string? message = null) => new(default, status, code, message ?? code);
    public static Response<T> Success(T data, int status = 200) => new(data, status);
}
