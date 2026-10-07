using System.Net;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Infrastructure.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class PlatformTrustedSourceAuditIntentClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.Created, false)]
    [InlineData(HttpStatusCode.OK, true)]
    public async Task Accepted_and_replay_receipts_use_bearer_only_exact_transport(HttpStatusCode status, bool duplicate)
    {
        var envelope = Envelope();
        var handler = new RecordingHandler(request => new HttpResponseMessage(status)
        {
            Content = new StringContent(ResponseJson(envelope, status, duplicate), Encoding.UTF8, "application/json")
        });
        var client = Client(handler);

        var result = await client.AcceptAsync(envelope, new("jwt-token", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Equal(TrustedSourceAuditIntentDeliveryOutcome.Accepted, result.Outcome);
        Assert.Equal(duplicate, result.Receipt!.Duplicate);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("jwt-token", handler.Request.Headers.Authorization.Parameter);
        Assert.Equal("https://platform.test/api/internal/v1/audit/source-intents/accept", handler.Request.RequestUri!.ToString());
        Assert.False(handler.Request.Headers.Contains("X-Tenant-Id"));
        Assert.DoesNotContain(handler.Request.Headers, header => header.Key.StartsWith("X-Audit-", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(18, body.RootElement.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TrustedSourceAuditIntentDeliveryOutcome.AuthenticationRejected)]
    [InlineData(HttpStatusCode.Forbidden, TrustedSourceAuditIntentDeliveryOutcome.AuthenticationRejected)]
    [InlineData(HttpStatusCode.Conflict, TrustedSourceAuditIntentDeliveryOutcome.Terminal)]
    [InlineData(HttpStatusCode.ServiceUnavailable, TrustedSourceAuditIntentDeliveryOutcome.Retryable)]
    [InlineData(HttpStatusCode.GatewayTimeout, TrustedSourceAuditIntentDeliveryOutcome.Retryable)]
    public async Task Statuses_are_classified_without_receipt(HttpStatusCode status, TrustedSourceAuditIntentDeliveryOutcome expected)
    {
        var result = await Client(new RecordingHandler(_ => new HttpResponseMessage(status)))
            .AcceptAsync(Envelope(), new("jwt", DateTimeOffset.UtcNow.AddMinutes(5)));
        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Receipt);
    }

    // BL-552 flow 3 FIX1 item 2: a 409 stays terminal, and Platform's own reason is handed on — the processor tells a
    // deploy-order gap (an operation not mapped yet) from a real conflict. A 409 without a readable reason has none.
    [Theory]
    [InlineData("{\"data\":null,\"statusCode\":409,\"isSuccessful\":false,\"errors\":[\"AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED\"],\"reason_code\":\"AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED\",\"correlation_id\":\"c\"}",
        "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED")]
    [InlineData("{\"reason_code\":\"not a code\"}", null)]
    [InlineData("not json", null)]
    public async Task A_conflict_carries_platforms_reason(string body, string? reason)
    {
        var result = await Client(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }))
            .AcceptAsync(Envelope(), new("jwt", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Equal(TrustedSourceAuditIntentDeliveryOutcome.Terminal, result.Outcome);
        Assert.Equal("AUDIT_SOURCE_INTENT_CONFLICT", result.ErrorCode);
        Assert.Equal(reason, result.CentralReasonCode);
    }

    // Flow 3 FIX2 K1: a 409 whose body is slow to arrive is still a definite 409 — the reason unknown, not a timeout.
    [Fact]
    public async Task A_conflict_whose_body_is_slow_stays_a_conflict_without_a_reason()
    {
        var result = await Client(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StreamContent(new NeverArrivingStream())
            }))
            .AcceptAsync(Envelope(), new("jwt", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Equal(TrustedSourceAuditIntentDeliveryOutcome.Terminal, result.Outcome);
        Assert.Equal("AUDIT_SOURCE_INTENT_CONFLICT", result.ErrorCode);
        Assert.Null(result.CentralReasonCode);
    }

    private sealed class NeverArrivingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Create_semantics_minus_one_to_zero_and_sequence_zero_are_accepted()
    {
        var envelope = Envelope() with { PreVersion = -1, PostVersion = 0, Sequence = 0 };
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(ResponseJson(envelope, HttpStatusCode.Created, false), Encoding.UTF8, "application/json")
        });

        var result = await Client(handler).AcceptAsync(envelope, new("jwt", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Equal(TrustedSourceAuditIntentDeliveryOutcome.Accepted, result.Outcome);
    }

    [Fact]
    public async Task Malformed_success_body_is_terminal_result_not_exception()
    {
        var result = await Client(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
        })).AcceptAsync(Envelope(), new("jwt", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Equal(TrustedSourceAuditIntentDeliveryOutcome.Terminal, result.Outcome);
        Assert.Null(result.Receipt);
    }

    [Fact]
    public void External_plaintext_platform_url_is_rejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            PlatformTrustedSourceAuditIntentClient.EnsureValidConfiguration(new()
            {
                PlatformBaseUrl = "http://platform.internal"
            }));

        Assert.Equal("AUDIT_SOURCE_INTENT_CLIENT_CONFIGURATION_INVALID", error.Message);
    }

    private static PlatformTrustedSourceAuditIntentClient Client(HttpMessageHandler handler) => new(
        new Factory(handler),
        Options.Create(new TrustedSourceAuditIntentClientOptions { PlatformBaseUrl = "https://platform.test" }),
        TimeProvider.System);

    internal static TrustedSourceAuditIntentEnvelope Envelope() => new(
        AuditIntentContract.SourceService, "mod-0290.audit-intent.v1", Guid.NewGuid(), Guid.NewGuid(),
        "GlobalProduct", Guid.NewGuid(), 0, 1, "GlobalProductDraftCreated", "actor", Guid.NewGuid(),
        "cause", "command", 1, DateTimeOffset.UtcNow.ToUniversalTime(), new string('A', 64), null, "source-key");

    private static string ResponseJson(TrustedSourceAuditIntentEnvelope envelope, HttpStatusCode status, bool duplicate) =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                centralAcknowledgement = "central-ack",
                centralIdempotencyKey = AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion),
                contractVersion = envelope.ContractVersion,
                acceptedAt = DateTimeOffset.UtcNow.ToUniversalTime(),
                duplicate
            },
            statusCode = (int)status,
            isSuccessful = true,
            errors = Array.Empty<string>(),
            reason_code = (string?)null,
            correlation_id = "correlation"
        });

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public byte[]? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return response(request);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
