using System.Text.Json.Serialization;

namespace Diten.Application.Common.Models;

public class Response<T>
{
    public bool Success { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public T? Data { get; set; }
    public ResponseError? Error { get; set; }

    [JsonIgnore]
    public int StatusCode { get; set; }

    public static Response<T> Ok(T data, int statusCode = 200, string correlationId = "") =>
        new()
        {
            Success = true,
            Data = data,
            StatusCode = statusCode,
            CorrelationId = correlationId
        };

    public static Response<T> Ok(T data, string correlationId) => Ok(data, 200, correlationId);

    public static Response<T> Fail(
        string errorCode,
        int statusCode = 400,
        string correlationId = "",
        Dictionary<string, List<string>>? details = null) =>
        new()
        {
            Success = false,
            StatusCode = statusCode,
            CorrelationId = correlationId,
            Error = new ResponseError
            {
                Code = errorCode,
                Details = details ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            }
        };

    public static Response<T> Fail(string errorCode, Dictionary<string, List<string>> details) =>
        Fail(errorCode, 400, string.Empty, details);
}

public sealed class ResponseError
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, List<string>> Details { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class ResultErrorCodes
{
    public const string NotFound = "NotFound";
    public const string Validation = "ValidationFailed";
    public const string Conflict = "Conflict";
}
