namespace Diten.CrmService.Application.Common.Models;

public sealed class Response<T>
{
    public T? Data { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public int StatusCode { get; init; }
    public bool IsSuccessful { get; init; }

    public static Response<T> Success(T data, int statusCode = 200)
        => new() { Data = data, StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> SuccessWithoutData(int statusCode = 200)
        => new() { StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Fail(string error, int statusCode = 400)
        => new() { Errors = [error], StatusCode = statusCode, IsSuccessful = false };

    public static Response<T> Fail(IReadOnlyList<string> errors, int statusCode = 400)
        => new() { Errors = errors, StatusCode = statusCode, IsSuccessful = false };
}
