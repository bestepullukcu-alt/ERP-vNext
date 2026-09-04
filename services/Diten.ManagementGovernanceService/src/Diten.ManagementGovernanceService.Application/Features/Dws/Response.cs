namespace Diten.ManagementGovernanceService.Application.Features.Dws;

public sealed class Response<T>
{
    public T? Data { get; private init; }
    public int StatusCode { get; private init; }
    public bool IsSuccessful { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static Response<T> Success(T data, int statusCode = 200) => new() { Data = data, StatusCode = statusCode, IsSuccessful = true };
    public static Response<T> Fail(string error, int statusCode) => new() { StatusCode = statusCode, Errors = [error] };
}
