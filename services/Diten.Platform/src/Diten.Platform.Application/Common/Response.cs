using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Common;

public sealed class Response<T>
{
    public T? Data { get; private init; }
    public int StatusCode { get; private init; }
    public bool IsSuccessful { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; private init; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; private init; }

    private Response() { }

    public static Response<T> Success(T data, int statusCode = 200, string? correlationId = null) =>
        new() { Data = data, StatusCode = statusCode, IsSuccessful = true, CorrelationId = correlationId };

    public static Response<T> Success(int statusCode = 200, string? correlationId = null) =>
        new() { StatusCode = statusCode, IsSuccessful = true, CorrelationId = correlationId };

    public static Response<T> Fail(
        string error,
        int statusCode = 400,
        string? reasonCode = null,
        string? correlationId = null) =>
        new()
        {
            StatusCode = statusCode,
            IsSuccessful = false,
            Errors = [error],
            ReasonCode = reasonCode,
            CorrelationId = correlationId
        };

    public static Response<T> Fail(
        IReadOnlyList<string> errors,
        int statusCode = 400,
        string? reasonCode = null,
        string? correlationId = null) =>
        new()
        {
            StatusCode = statusCode,
            IsSuccessful = false,
            Errors = errors,
            ReasonCode = reasonCode,
            CorrelationId = correlationId
        };
}

public readonly record struct NoContent;
