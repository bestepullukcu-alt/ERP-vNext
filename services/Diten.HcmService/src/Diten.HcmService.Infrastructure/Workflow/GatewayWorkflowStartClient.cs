using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.HcmService.Infrastructure.Workflow;

public sealed class GatewayWorkflowStartClient : IWorkflowStartClient
{
    private const string StartPath = "/api/v1/platform/workflows/instances";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string AuthorizationHeaderName = "Authorization";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GatewayWorkflowStartClient> _logger;

    public GatewayWorkflowStartClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GatewayWorkflowStartClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5000");
    }

    public async Task<WorkflowStartClientResult> StartAsync(
        WorkflowStartClientRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, StartPath)
            {
                Content = JsonContent.Create(request)
            };
            ForwardContextHeaders(message);

            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return WorkflowStartClientResult.Failed(
                    (int)response.StatusCode,
                    $"workflow_start_http_{(int)response.StatusCode}");
            }

            var payload = await ReadResponseAsync(response, cancellationToken);
            if (payload.WorkflowInstanceId is null || string.IsNullOrWhiteSpace(payload.DefinitionKey))
            {
                return WorkflowStartClientResult.Failed(
                    (int)response.StatusCode,
                    "workflow_start_response_invalid");
            }

            return WorkflowStartClientResult.Success(
                (int)response.StatusCode,
                payload.WorkflowInstanceId.Value,
                payload.DefinitionKey,
                payload.DefinitionVersion ?? 1,
                payload.Status ?? "pending_approval",
                payload.ETag);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Workflow start dependency unavailable for {DefinitionKey}.", request.DefinitionKey);
            return WorkflowStartClientResult.Failed(
                (int)HttpStatusCode.ServiceUnavailable,
                "workflow_start_dependency_unavailable");
        }
    }

    private void ForwardContextHeaders(HttpRequestMessage message)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        if (httpContext.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeader))
        {
            message.Headers.TryAddWithoutValidation(TenantHeaderName, tenantHeader.ToArray());
        }

        if (httpContext.Request.Headers.TryGetValue(AuthorizationHeaderName, out var authorizationHeader))
        {
            message.Headers.TryAddWithoutValidation(AuthorizationHeaderName, authorizationHeader.ToArray());
        }

        if (httpContext.Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationHeader))
        {
            message.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationHeader.ToArray());
        }
    }

    private static async Task<WorkflowStartResponseBody> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return new WorkflowStartResponseBody
            {
                WorkflowInstanceId = TryFindGuid(document.RootElement, "workflowInstanceId"),
                DefinitionKey = TryFindString(document.RootElement, "definitionKey"),
                DefinitionVersion = TryFindInt(document.RootElement, "definitionVersion"),
                Status = TryFindString(document.RootElement, "status"),
                ETag = TryFindString(document.RootElement, "etag")
            };
        }
        catch (JsonException)
        {
            return new WorkflowStartResponseBody();
        }
    }

    private static string? TryFindString(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }

            var nested = TryFindString(property.Value, key);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Guid? TryFindGuid(JsonElement element, string key)
        => Guid.TryParse(TryFindString(element, key), out var parsed) ? parsed : null;

    private static int? TryFindInt(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetInt32(out var value))
            {
                return value;
            }

            var nested = TryFindInt(property.Value, key);
            if (nested.HasValue)
            {
                return nested;
            }
        }

        return null;
    }

    private sealed record WorkflowStartResponseBody
    {
        public Guid? WorkflowInstanceId { get; init; }
        public string? DefinitionKey { get; init; }
        public int? DefinitionVersion { get; init; }
        public string? Status { get; init; }
        public string? ETag { get; init; }
    }
}
