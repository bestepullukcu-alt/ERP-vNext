using System.Reflection;
using System.Text.Json;
using Diten.Platform.API.Middleware;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Audit;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Middleware;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX2 — how a refused audit record is answered over HTTP, and that the MediatR pipeline lets
/// the refusal through as itself.
/// </summary>
public sealed class AuditRefusalHttpMappingTests
{
    private const string InternalSentence = "In-transaction audit for SomeCommand could not name who made it.";

    [Fact]
    public async Task A_change_with_nobody_to_name_is_503_with_its_code_and_without_the_internal_sentence()
    {
        var (status, body, raw) = await HandleAsync(new TransactionOwnedAuditRefusedException(InternalSentence));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Equal("AUDIT_RECORD_UNAVAILABLE", body.GetProperty("reason_code").GetString());
        Assert.DoesNotContain("could not name", raw);
    }

    [Fact]
    public async Task An_invalid_intent_is_a_server_error_with_its_own_code_never_try_later()
    {
        // FIX2 item 7 — no intent, no category, an empty target tenant: a retry can never cure these.
        var (status, body, raw) = await HandleAsync(new TransactionOwnedAuditIntentInvalidException("In-transaction audit for X names no category."));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("AUDIT_INTENT_INVALID", body.GetProperty("reason_code").GetString());
        Assert.DoesNotContain("names no category", raw);
    }

    [Fact]
    public async Task The_pipeline_lets_an_audit_refusal_through_even_when_its_Fail_lookup_succeeds()
    {
        // FIX2 item 8 — the refusal used to reach the API's handler only because ExceptionBehavior's reflective lookup of
        // Fail(string, int) found nothing. Here the lookup is made to SUCCEED (flow-local seam): the refusal must still
        // pass through as itself, not become a 400 response carrying the internal sentence.
        ExceptionBehavior<Probe, Response<NoContent>>.FailMethodFinderOverride.Value =
            _ => typeof(AuditRefusalHttpMappingTests).GetMethod(nameof(FailWithStatus), BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            var behavior = new ExceptionBehavior<Probe, Response<NoContent>>(NullLogger<ExceptionBehavior<Probe, Response<NoContent>>>.Instance);

            // the seam works: an ordinary InvalidOperationException IS turned into a response through it …
            var ordinary = await behavior.Handle(new Probe(), () => throw new InvalidOperationException("plain"), CancellationToken.None);
            Assert.Equal(400, ordinary.StatusCode);

            // … and the audit refusals are not
            await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() =>
                behavior.Handle(new Probe(), () => throw new TransactionOwnedAuditRefusedException(InternalSentence), CancellationToken.None));
            await Assert.ThrowsAsync<TransactionOwnedAuditIntentInvalidException>(() =>
                behavior.Handle(new Probe(), () => throw new TransactionOwnedAuditIntentInvalidException("bad intent"), CancellationToken.None));
        }
        finally
        {
            ExceptionBehavior<Probe, Response<NoContent>>.FailMethodFinderOverride.Value = null;
        }
    }

    private static Response<NoContent> FailWithStatus(string error, int statusCode) => Response<NoContent>.Fail(error, statusCode);

    public sealed record Probe : IRequest<Response<NoContent>>;

    private static async Task<(int Status, JsonElement Body, string Raw)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        Assert.True(await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance).TryHandleAsync(context, exception, CancellationToken.None));

        responseBody.Position = 0;
        var raw = new StreamReader(responseBody).ReadToEnd();
        return (context.Response.StatusCode, JsonDocument.Parse(raw).RootElement, raw);
    }
}
