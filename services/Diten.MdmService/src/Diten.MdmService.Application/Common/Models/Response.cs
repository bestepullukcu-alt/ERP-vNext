namespace Diten.Shared.Core;

public sealed class Response<T>
{
    public T? Data { get; private set; }
    public int StatusCode { get; private set; }
    public bool IsSuccessful { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = [];

    private Response() { }

    public static Response<T> Success(T data, int statusCode = 200)
        => new() { Data = data, StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> SuccessWithoutData(int statusCode = 200)
        => new() { StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Fail(string error, int statusCode = 400)
        => new() { StatusCode = statusCode, IsSuccessful = false, Errors = [error] };

    public static Response<T> Fail(IReadOnlyList<string> errors, int statusCode = 400)
        => new() { StatusCode = statusCode, IsSuccessful = false, Errors = errors };
}

public sealed class NoContent;
