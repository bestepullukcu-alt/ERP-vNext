using System.Net;
using System.Text;
using System.Text.Json;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Infrastructure.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class GovernedHcmAuditAppendClientTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task AppendAsync_authoritative_success_allows_activation_grade_success()
    {
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.Created, new
        {
            data = new
            {
                status = "Queued",
                authoritativePersistenceAccepted = true,
                shouldBlockBusinessCommand = false
            }
        }));
        var client = CreateClient(handler);
        var request = CreateSafeRequest();

        var result = await client.AppendAsync(request, CancellationToken.None);

        Assert.True(result.AuthoritativePersistenceAccepted);
        Assert.True(result.AllowsActivationGradeSuccess);
        Assert.False(result.ShouldBlockActivationGradeOperation);
        Assert.Equal(201, result.HttpStatusCode);
        Assert.Equal("Queued", result.ProviderStatus);
        Assert.Equal("/api/v1/platform/audit/events", handler.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer redacted", handler.AuthorizationHeader);
        Assert.Equal(TenantId.ToString("D"), handler.TenantHeader);
        Assert.Equal(request.CorrelationId.ToString("D"), handler.CorrelationHeader);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var root = document.RootElement;
        Assert.Equal(request.CorrelationId, root.GetProperty("correlationId").GetGuid());
        Assert.Equal(request.RequestType, root.GetProperty("requestType").GetString());
        Assert.Equal("TenantUser", root.GetProperty("actorType").GetString());
        Assert.Equal(TenantId, root.GetProperty("targetTenantId").GetGuid());
        Assert.Equal("Diten.HcmService", root.GetProperty("sourceService").GetString());
        Assert.Equal("MOD-0251", root.GetProperty("sourceModule").GetString());
        Assert.Equal("Employee", root.GetProperty("entityType").GetString());
        Assert.Equal("Update", root.GetProperty("operation").GetString());
        Assert.Equal("Succeeded", root.GetProperty("outcome").GetString());
        Assert.True(root.GetProperty("metadata").TryGetProperty("changed_fields", out _));
        var requestBody = Assert.IsType<string>(handler.RequestBody);
        Assert.False(requestBody.Contains("government_identifier", StringComparison.OrdinalIgnoreCase));
        Assert.False(requestBody.Contains("before", StringComparison.OrdinalIgnoreCase));
        Assert.False(requestBody.Contains("after", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AppendAsync_non_success_status_blocks_activation_grade_success(HttpStatusCode statusCode)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(statusCode));
        var client = CreateClient(handler);

        var result = await client.AppendAsync(CreateSafeRequest(), CancellationToken.None);

        Assert.False(result.AuthoritativePersistenceAccepted);
        Assert.False(result.AllowsActivationGradeSuccess);
        Assert.True(result.ShouldBlockActivationGradeOperation);
        Assert.Equal((int)statusCode, result.HttpStatusCode);
        Assert.Equal($"audit_append_http_{(int)statusCode}", result.ReasonCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task AppendAsync_false_or_missing_authoritative_acceptance_blocks(bool? accepted)
    {
        object body = accepted.HasValue
            ? new { data = new { status = "Queued", authoritativePersistenceAccepted = accepted.Value } }
            : new { data = new { status = "Queued" } };

        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.OK, body));
        var client = CreateClient(handler);

        var result = await client.AppendAsync(CreateSafeRequest(), CancellationToken.None);

        Assert.False(result.AuthoritativePersistenceAccepted);
        Assert.False(result.AllowsActivationGradeSuccess);
        Assert.True(result.ShouldBlockActivationGradeOperation);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal("authoritative_persistence_not_accepted", result.ReasonCode);
    }

    [Fact]
    public async Task AppendAsync_dependency_unavailable_blocks_activation_grade_success()
    {
        var handler = new RecordingHandler(new HttpRequestException("offline"));
        var client = CreateClient(handler);

        var result = await client.AppendAsync(CreateSafeRequest(), CancellationToken.None);

        Assert.False(result.AuthoritativePersistenceAccepted);
        Assert.False(result.AllowsActivationGradeSuccess);
        Assert.True(result.ShouldBlockActivationGradeOperation);
        Assert.Equal(503, result.HttpStatusCode);
        Assert.Equal("audit_append_dependency_unavailable", result.ReasonCode);
    }

    [Fact]
    public async Task AppendAsync_rejects_unsafe_metadata_before_send()
    {
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.Created, new
        {
            data = new { authoritativePersistenceAccepted = true }
        }));
        var client = CreateClient(handler);
        var request = CreateSafeRequest() with
        {
            Metadata = new Dictionary<string, object?>
            {
                ["government_identifier_token"] = "redacted-in-test"
            }
        };

        var result = await client.AppendAsync(request, CancellationToken.None);

        Assert.Equal(0, handler.SendCount);
        Assert.False(result.AllowsActivationGradeSuccess);
        Assert.Equal("metadata_contains_prohibited_key", result.ReasonCode);
    }

    [Fact]
    public async Task AppendAsync_tenant_header_mismatch_fails_closed_before_send()
    {
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.Created, new
        {
            data = new { authoritativePersistenceAccepted = true }
        }));
        var client = CreateClient(handler, headerTenantId: Guid.Parse("10000000-0000-0000-0000-000000000001"));

        var result = await client.AppendAsync(CreateSafeRequest(), CancellationToken.None);

        Assert.Equal(0, handler.SendCount);
        Assert.False(result.AllowsActivationGradeSuccess);
        Assert.Equal(403, result.HttpStatusCode);
        Assert.Equal("target_tenant_mismatch", result.ReasonCode);
    }

    [Fact]
    public void Logger_only_fallback_result_cannot_satisfy_activation_grade_success()
    {
        var result = HcmAuditAppendResult.Blocked("logger_only_fallback");

        Assert.False(result.AuthoritativePersistenceAccepted);
        Assert.True(result.ShouldBlockActivationGradeOperation);
        Assert.False(result.AllowsActivationGradeSuccess);
    }

    private static GovernedHcmAuditAppendClient CreateClient(
        RecordingHandler handler,
        Guid? headerTenantId = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:BaseUrl"] = "http://localhost:5000"
            })
            .Build();

        var tenantId = headerTenantId ?? TenantId;
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString("D");
        httpContext.Request.Headers["Authorization"] = "Bearer redacted";
        httpContext.Request.Headers["X-Correlation-Id"] = "20000000-0000-0000-0000-000000000001";

        return new GovernedHcmAuditAppendClient(
            new HttpClient(handler),
            configuration,
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<GovernedHcmAuditAppendClient>.Instance);
    }

    private static HcmAuditAppendRequest CreateSafeRequest()
        => EmployeeAuditAdapterMapper.MapEmployeeEvent(
            EmployeeAuditPayloadBuilder.ProfileUpdated(
                TenantId,
                Guid.Parse("30000000-0000-0000-0000-000000000001"),
                Guid.Parse("40000000-0000-0000-0000-000000000001"),
                Guid.Parse("20000000-0000-0000-0000-000000000001").ToString("D"),
                "hash-1",
                ["employee_status", "sensitivity_level"],
                3),
            DateTimeOffset.Parse("2026-06-20T00:00:00Z"));

    private static StringContent JsonResponseContent(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object value)
        => new(statusCode)
        {
            Content = JsonResponseContent(value)
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public RecordingHandler(Exception exception)
        {
            _exception = exception;
        }

        public int SendCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public string? AuthorizationHeader { get; private set; }
        public string? TenantHeader { get; private set; }
        public string? CorrelationHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            RequestUri = request.RequestUri;
            AuthorizationHeader = request.Headers.TryGetValues("Authorization", out var authorization)
                ? authorization.Single()
                : null;
            TenantHeader = request.Headers.TryGetValues("X-Tenant-Id", out var tenant)
                ? tenant.Single()
                : null;
            CorrelationHeader = request.Headers.TryGetValues("X-Correlation-Id", out var correlation)
                ? correlation.Single()
                : null;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_exception is not null)
            {
                throw _exception;
            }

            return _response!;
        }
    }
}
