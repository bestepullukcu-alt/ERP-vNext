using System.Net;
using System.Text;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Infrastructure.Services.ESignature;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E2 — Platform's client of AuthService's internal consume (E1, <c>internal/esign-tickets/consume</c>): the path
/// E1 actually serves (not <c>api/internal/…</c>), the internal key header, 409 → invalid, anything else → fail closed.
/// </summary>
public sealed class AuthServiceESignTicketConsumerTests
{
    [Fact]
    public async Task A_consumed_ticket_is_read_from_AuthServices_envelope()
    {
        var edge = new Edge(HttpStatusCode.OK,
            "{\"data\":{\"kind\":\"Window\",\"intent\":\"sign\",\"passwordVerifiedAtUtc\":\"2026-10-08T09:00:00Z\",\"issuedAtUtc\":\"2026-10-08T09:05:00Z\"}}");
        var result = await Consumer(edge, "key-123").ConsumeAsync("ticket-value", Guid.NewGuid(), Guid.NewGuid(), "sign", CancellationToken.None);

        Assert.Equal(TicketConsumeOutcome.Consumed, result.Outcome);
        Assert.Equal(ESignatureReauthKind.Window, result.Kind);
        Assert.Equal(new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc), result.PasswordVerifiedAtUtc);
        Assert.Equal("/internal/esign-tickets/consume", edge.Path);
        Assert.Equal("key-123", edge.InternalKey);
        Assert.Contains("\"intent\":\"sign\"", edge.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, TicketConsumeOutcome.Invalid)]
    [InlineData(HttpStatusCode.Unauthorized, TicketConsumeOutcome.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, TicketConsumeOutcome.Unavailable)]
    [InlineData(HttpStatusCode.NotFound, TicketConsumeOutcome.Unavailable)]
    public async Task Only_409_means_invalid_everything_else_fails_closed(HttpStatusCode status, TicketConsumeOutcome expected)
    {
        var result = await Consumer(new Edge(status, "{}"), "key").ConsumeAsync("t", Guid.NewGuid(), Guid.NewGuid(), "sign", CancellationToken.None);
        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task Without_an_internal_key_nothing_is_sent_and_signing_fails_closed()
    {
        var edge = new Edge(HttpStatusCode.OK, "{}");
        var result = await Consumer(edge, "").ConsumeAsync("t", Guid.NewGuid(), Guid.NewGuid(), "sign", CancellationToken.None);
        Assert.Equal(TicketConsumeOutcome.Unavailable, result.Outcome);
        Assert.Null(edge.Path);
    }

    private static AuthServiceESignTicketConsumer Consumer(Edge edge, string key) =>
        new(new HttpClient(edge), Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test:5056", InternalApiKey = key }),
            NullLogger<AuthServiceESignTicketConsumer>.Instance);

    private sealed class Edge(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? InternalKey { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            InternalKey = request.Headers.TryGetValues("X-Internal-Api-Key", out var values) ? values.Single() : null;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
