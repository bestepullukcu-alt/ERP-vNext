using System.Net;
using System.Text;
using System.Text.Json;
using Diten.HcmService.Infrastructure.ReferenceValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class GatewayReferenceValidationClientTests
{
    [Fact]
    public async Task ValidatePersonAsync_posts_personIds_array_contract()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new
            {
                data = new
                {
                    results = new[]
                    {
                        new
                        {
                            personId = "02510000-0000-0000-0000-000000000001",
                            referenceable = true,
                            displayName = "MOD0251 Smoke Person",
                            status = "Active"
                        }
                    }
                }
            })
        });
        var client = CreateClient(handler);

        var result = await client.ValidatePersonAsync("02510000-0000-0000-0000-000000000001", CancellationToken.None);

        Assert.True(result.IsReferenceable);
        Assert.Equal("valid", result.Status);
        Assert.Equal("/api/v1/platform/persons/lookup-validation", handler.RequestUri!.PathAndQuery);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        Assert.True(document.RootElement.TryGetProperty("personIds", out var personIds));
        Assert.Equal(JsonValueKind.Array, personIds.ValueKind);
        Assert.Equal("02510000-0000-0000-0000-000000000001", personIds[0].GetString());
        Assert.False(document.RootElement.TryGetProperty("personId", out _));
    }

    [Fact]
    public async Task ValidatePersonAsync_missing_person_still_blocks_without_provider_call()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var result = await client.ValidatePersonAsync(null, CancellationToken.None);

        Assert.False(result.IsReferenceable);
        Assert.Equal("missing", result.Status);
        Assert.Equal("missing_reference", result.ReasonCode);
        Assert.Equal(0, handler.SendCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "permission_denied")]
    [InlineData(HttpStatusCode.Unauthorized, "permission_denied")]
    [InlineData(HttpStatusCode.NotFound, "not_found_or_tenant_mismatch")]
    [InlineData(HttpStatusCode.BadRequest, "provider_rejected")]
    public async Task ValidatePersonAsync_preserves_provider_error_mapping(HttpStatusCode statusCode, string expectedReasonCode)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(statusCode));
        var client = CreateClient(handler);

        var result = await client.ValidatePersonAsync("02510000-0000-0000-0000-000000000001", CancellationToken.None);

        Assert.False(result.IsReferenceable);
        Assert.Equal("blocked", result.Status);
        Assert.Equal(expectedReasonCode, result.ReasonCode);
    }

    private static GatewayReferenceValidationClient CreateClient(RecordingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:BaseUrl"] = "http://localhost:5000"
            })
            .Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = "00000000-0000-0000-0000-000000000001";
        httpContext.Request.Headers["Authorization"] = "Bearer redacted";

        return new GatewayReferenceValidationClient(
            new HttpClient(handler),
            configuration,
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<GatewayReferenceValidationClient>.Instance);
    }

    private static StringContent JsonContent(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public int SendCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _response;
        }
    }
}
