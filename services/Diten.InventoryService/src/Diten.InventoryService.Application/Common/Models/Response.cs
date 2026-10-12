namespace Diten.InventoryService.Application.Common.Models;

/// <summary>
/// Internal handler result (repo <c>Response&lt;T&gt;</c> pattern). Pack §8 limits the wire to the unwrapped
/// INVENTORY-BUNDLE schemas, so this type never reaches the wire: the controller unwraps <see cref="Data"/> on
/// success and renders the contract <c>Error</c> from <see cref="ErrorCode"/> / <see cref="Errors"/> /
/// <see cref="ErrorDetails"/> on failure.
/// </summary>
public sealed class Response<T>
{
    public T? Data { get; private set; }

    public int StatusCode { get; private set; }

    public bool IsSuccessful { get; private set; }

    public IReadOnlyList<string> Errors { get; private set; } = [];

    /// <summary>Contract <c>error.code</c>; null only for failures that have no declared code.</summary>
    public string? ErrorCode { get; private set; }

    public IReadOnlyDictionary<string, object?>? ErrorDetails { get; private set; }

    private Response() { }

    public static Response<T> Success(T data, int statusCode = 200)
        => new() { Data = data, StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Fail(string error, int statusCode = 400)
        => new() { StatusCode = statusCode, IsSuccessful = false, Errors = [error] };

    public static Response<T> Fail(IReadOnlyList<string> errors, int statusCode = 400)
        => new() { StatusCode = statusCode, IsSuccessful = false, Errors = errors };

    public static Response<T> Fail(
        string errorCode,
        string error,
        int statusCode,
        IReadOnlyDictionary<string, object?>? details = null)
        => new()
        {
            StatusCode = statusCode,
            IsSuccessful = false,
            Errors = [error],
            ErrorCode = errorCode,
            ErrorDetails = details
        };

    public static Response<T> Fail(string errorCode, IReadOnlyList<string> errors, int statusCode)
        => new() { StatusCode = statusCode, IsSuccessful = false, Errors = errors, ErrorCode = errorCode };
}
