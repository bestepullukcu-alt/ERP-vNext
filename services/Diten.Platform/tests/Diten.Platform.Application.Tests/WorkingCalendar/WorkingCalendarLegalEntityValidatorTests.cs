using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services.Mdm;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

public sealed class WorkingCalendarLegalEntityValidatorTests
{
    [Fact]
    public async Task Transient_response_retries_once_and_propagates_required_headers()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var requests = new List<HttpRequestMessage>();
        var handler = new StubHandler((request, attempt) =>
        {
            requests.Add(CloneHeaders(request));
            return attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json(HttpStatusCode.OK,
                    JsonSerializer.Serialize(new
                    {
                        data = new
                        {
                            legalEntityId = id,
                            code = "LE",
                            legalName = "Legal",
                            displayName = "Legal",
                            lifecycleState = "ACTIVE",
                            referenceable = true
                        },
                        statusCode = 200,
                        isSuccessful = true,
                        errors = Array.Empty<string>()
                    }));
        });
        var context = new DefaultHttpContext { TraceIdentifier = "trace-fallback" };
        context.Request.Headers.Authorization = "Bearer token-value";
        context.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        context.Request.Headers["X-Correlation-Id"] = "corr-123";

        var validator = CreateValidator(handler, context);
        var result = await validator.ValidateAsync(id);

        Assert.True(result.IsReferenceable);
        Assert.False(result.DependencyUnavailable);
        Assert.Equal(2, handler.CallCount);
        Assert.All(requests, request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("token-value", request.Headers.Authorization?.Parameter);
            Assert.Equal(tenantId.ToString(), request.Headers.GetValues("X-Tenant-Id").Single());
            Assert.Equal("corr-123", request.Headers.GetValues("X-Correlation-Id").Single());
        });
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{}")]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.OK, "not-json")]
    [InlineData(HttpStatusCode.OK, "{\"data\":null,\"statusCode\":200,\"isSuccessful\":true,\"errors\":[]}")]
    public async Task Non_transient_or_malformed_validation_fails_closed_without_retry(
        HttpStatusCode status, string content)
    {
        var handler = new StubHandler((_, _) => Json(status, content));
        var validator = CreateValidator(handler, new DefaultHttpContext());

        var result = await validator.ValidateAsync(Guid.NewGuid());

        Assert.False(result.IsReferenceable);
        Assert.True(result.DependencyUnavailable);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Only_governed_transient_statuses_get_exactly_one_retry(HttpStatusCode status)
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(status));
        var validator = CreateValidator(handler, new DefaultHttpContext());

        var result = await validator.ValidateAsync(Guid.NewGuid());

        Assert.True(result.DependencyUnavailable);
        Assert.Equal(2, handler.CallCount);
    }

    private static WorkingCalendarLegalEntityValidator CreateValidator(
        HttpMessageHandler handler, HttpContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" })
            .Build();
        var tenant = new Mock<ITenantContext>(MockBehavior.Strict);
        tenant.SetupGet(x => x.IsResolved).Returns(false);
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new WorkingCalendarLegalEntityValidator(new HttpClient(handler), configuration, accessor, tenant.Object);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static HttpRequestMessage CloneHeaders(HttpRequestMessage source)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri);
        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response(request, CallCount));
        }
    }
}
