namespace Diten.AuthService.Application.Common;

public sealed class Response<T>
{
    public T? Data { get; private init; }
    public int StatusCode { get; private init; }
    public bool IsSuccessful { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    // Machine-readable, localizable failure descriptors carried ALONGSIDE the human-readable Errors (English
    // fallback). The MVC frontend resolves each code -> a culture-specific string; this is empty for every
    // non-coded failure, in which case consumers fall back to Errors. Additive/optional — never breaks Errors.
    public IReadOnlyList<ResponseError> ErrorCodes { get; private init; } = [];

    private Response() { }

    public static Response<T> Success(T data, int statusCode = 200) =>
        new() { Data = data, StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Success(int statusCode = 200) =>
        new() { StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Fail(string error, int statusCode = 400) =>
        new() { StatusCode = statusCode, IsSuccessful = false, Errors = [error] };

    public static Response<T> Fail(IReadOnlyList<string> errors, int statusCode = 400) =>
        new() { StatusCode = statusCode, IsSuccessful = false, Errors = errors };

    public static Response<T> Fail(string error, IReadOnlyList<ResponseError> errorCodes, int statusCode = 400) =>
        new() { StatusCode = statusCode, IsSuccessful = false, Errors = [error], ErrorCodes = errorCodes };
}

// A stable, machine-readable validation failure: a dot-namespaced Code plus optional string-valued Params
// (e.g. { "minLength": "10" }). The frontend maps Code -> a localized template and substitutes the param.
public sealed record ResponseError(string Code, IReadOnlyDictionary<string, string>? Params = null);

public readonly record struct NoContent;
