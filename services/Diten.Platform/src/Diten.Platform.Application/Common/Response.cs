namespace Diten.Platform.Application.Common;

public sealed class Response<T>
{
    public T? Data { get; private init; }
    public int StatusCode { get; private init; }
    public bool IsSuccessful { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    private Response() { }

    public static Response<T> Success(T data, int statusCode = 200) =>
        new() { Data = data, StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Success(int statusCode = 200) =>
        new() { StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Fail(string error, int statusCode = 400) =>
        new() { StatusCode = statusCode, IsSuccessful = false, Errors = [error] };

    public static Response<T> Fail(IReadOnlyList<string> errors, int statusCode = 400) =>
        new() { StatusCode = statusCode, IsSuccessful = false, Errors = errors };
}

public readonly record struct NoContent;
