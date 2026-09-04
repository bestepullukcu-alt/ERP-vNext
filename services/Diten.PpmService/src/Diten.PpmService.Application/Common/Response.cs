namespace Diten.Shared.Core;

public sealed class Response<T>
{
    public T? Data { get; private init; }
    public int StatusCode { get; private init; }
    public bool IsSuccessful { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];
    private Response() { }
    public static Response<T> Success(T data, int statusCode = 200) => new() { Data = data, StatusCode = statusCode, IsSuccessful = true };
    public static Response<T> SuccessWithoutData(int statusCode = 204) => new() { StatusCode = statusCode, IsSuccessful = true };
    public static Response<T> Fail(string error, int statusCode = 400) => new() { StatusCode = statusCode, Errors = [error] };
    public static Response<T> Fail(IReadOnlyList<string> errors, int statusCode = 400) => new() { StatusCode = statusCode, Errors = errors };
}
