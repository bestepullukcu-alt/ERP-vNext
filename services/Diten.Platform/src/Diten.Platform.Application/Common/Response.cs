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

    /// <summary>
    /// WP-EMAIL-LANG-01 FIX1 — a SUCCESS that is not complete: the work was done for some and not for others (e.g. some of a
    /// notification's language groups were queued). <paramref name="reasonCode"/> says so and <paramref name="errors"/>
    /// names what was not done. Named apart from <c>Success</c> / <c>Fail</c> so it is never picked by accident.
    /// </summary>
    public static Response<T> PartialSuccess(T data, string reasonCode, IReadOnlyList<string> errors, int statusCode = 200) =>
        new() { Data = data, StatusCode = statusCode, IsSuccessful = true, ReasonCode = reasonCode, Errors = errors };

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

    /// <summary>
    /// A refusal that carries DATA the caller needs to act on — e.g. which block a plan collided with
    /// (WP-TASK-CALENDAR-ENGINE-01, <c>TASK_PLAN_CONFLICT</c>). Named apart from <see cref="Fail(string,int,string?,string?)"/>
    /// so it can never be picked by overload resolution by accident.
    /// </summary>
    public static Response<T> FailWithData(
        string error,
        int statusCode,
        string? reasonCode,
        T data,
        string? correlationId = null) =>
        new()
        {
            Data = data,
            StatusCode = statusCode,
            IsSuccessful = false,
            Errors = [error],
            ReasonCode = reasonCode,
            CorrelationId = correlationId
        };
}

public readonly record struct NoContent;
