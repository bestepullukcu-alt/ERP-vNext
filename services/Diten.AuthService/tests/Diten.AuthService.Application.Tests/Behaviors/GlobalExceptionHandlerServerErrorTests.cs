using System.Text.Json;
using Diten.AuthService.Api;
using Diten.AuthService.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Tests.Behaviors;

/// <summary>
/// BL-516 (CONTROL TOWER, acceptance of WP-USERS-ERROR-CODES-01) — what an UNEXPECTED failure says on the wire.
/// <see cref="GlobalExceptionHandler"/> used to write <c>detail = exception.Message</c> for every status. On a 500
/// that message is a driver's or a library's own text (a Mongo timeout names host and port), and the list, export and
/// delete requests of the Users screen go browser → gateway directly, so the body is readable in the network tab.
/// A 5xx now carries one fixed sentence and the trace id; the exception itself goes to the server log, in full.
/// A 4xx is the application's own answer and keeps its text.
/// </summary>
public sealed class GlobalExceptionHandlerServerErrorTests
{
    private const string Leak = "MongoConnectionException: auth-db.internal:27017 timed out after 30000ms";

    [Fact]
    public async Task An_unexpected_exception_is_500_with_a_fixed_sentence_and_none_of_its_own_text()
    {
        var (status, body, log) = await HandleAsync(new InvalidOperationException(Leak));

        Assert.Equal(500, status);
        Assert.Equal(["title", "status", "detail", "traceId"], body.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("Server error", body.GetProperty("title").GetString());
        Assert.Equal(GlobalExceptionHandler.ServerErrorDetail, body.GetProperty("detail").GetString());
        Assert.DoesNotContain("auth-db.internal", body.GetRawText());
        Assert.DoesNotContain("MongoConnectionException", body.GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        // Nothing is lost: the exception, with its message, is what the server log receives.
        Assert.Contains(log.Exceptions, e => e.Message == Leak);
    }

    [Fact]
    public async Task A_status_exception_of_5xx_is_treated_the_same_way()
    {
        var (status, body, _) = await HandleAsync(new HttpStatusException(502, Leak));

        Assert.Equal(502, status);
        Assert.Equal(GlobalExceptionHandler.ServerErrorDetail, body.GetProperty("detail").GetString());
        Assert.DoesNotContain("auth-db.internal", body.GetRawText());
    }

    [Fact]
    public async Task A_4xx_keeps_the_applications_own_sentence()
    {
        // The control: the fixed sentence is the 5xx rule, not something every refusal gets.
        var (status, body, _) = await HandleAsync(new HttpStatusException(409, "The role is in use."));

        Assert.Equal(409, status);
        Assert.Equal("The role is in use.", body.GetProperty("detail").GetString());

        var (validationStatus, validationBody, _) = await HandleAsync(new ValidationException([new ValidationFailure("Kind", "Kind must be one of: Unknown, Human, Service.")]));

        Assert.Equal(400, validationStatus);
        Assert.Contains("Kind must be one of", validationBody.GetProperty("detail").GetString());
    }

    private static async Task<(int Status, JsonElement Body, CapturedLog Log)> HandleAsync(Exception exception)
    {
        var log = new CapturedLog();
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        http.Response.Body = new MemoryStream();

        Assert.True(await new GlobalExceptionHandler(log).TryHandleAsync(http, exception, CancellationToken.None));

        http.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(http.Response.Body);
        return (http.Response.StatusCode, doc.RootElement.Clone(), log);
    }

    private sealed class CapturedLog : ILogger<GlobalExceptionHandler>
    {
        public List<Exception> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
            {
                Exceptions.Add(exception);
            }
        }
    }
}
