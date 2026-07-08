using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.HcmService.Infrastructure.Audit;

public sealed class GovernedHcmAuditAppendClient : IHcmAuditAppendClient
{
    private const string AppendPath = "/api/v1/platform/audit/events";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string AuthorizationHeaderName = "Authorization";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GovernedHcmAuditAppendClient> _logger;

    public GovernedHcmAuditAppendClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GovernedHcmAuditAppendClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        var gatewayBaseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl);
    }

    public async Task<HcmAuditAppendResult> AppendAsync(
        HcmAuditAppendRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TargetTenantId == Guid.Empty)
        {
            return HcmAuditAppendResult.Blocked("target_tenant_required");
        }

        if (!EmployeeAuditAdapterMapper.IsSafeMetadata(request.Metadata))
        {
            return HcmAuditAppendResult.Blocked("metadata_contains_prohibited_key");
        }

        if (TryGetHeaderTenantId(out var headerTenantId) && headerTenantId != request.TargetTenantId)
        {
            return HcmAuditAppendResult.Blocked("target_tenant_mismatch", (int)HttpStatusCode.Forbidden);
        }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, AppendPath)
            {
                Content = JsonContent.Create(ToGatewayRequest(request))
            };
            ForwardContextHeaders(message, request);

            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return HcmAuditAppendResult.Blocked(
                    $"audit_append_http_{(int)response.StatusCode}",
                    (int)response.StatusCode);
            }

            var appendResponse = await ReadAppendResponseAsync(response, cancellationToken);
            if (appendResponse.AuthoritativePersistenceAccepted)
            {
                return HcmAuditAppendResult.Accepted(
                    (int)response.StatusCode,
                    appendResponse.Status);
            }

            return HcmAuditAppendResult.Blocked(
                "authoritative_persistence_not_accepted",
                (int)response.StatusCode,
                appendResponse.Status);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Governed audit append dependency unavailable for {RequestType}.", request.RequestType);
            return HcmAuditAppendResult.Blocked("audit_append_dependency_unavailable", (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private void ForwardContextHeaders(HttpRequestMessage message, HcmAuditAppendRequest request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            message.Headers.TryAddWithoutValidation(CorrelationHeaderName, request.CorrelationId.ToString("D"));
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
        else
        {
            message.Headers.TryAddWithoutValidation(CorrelationHeaderName, request.CorrelationId.ToString("D"));
        }
    }

    private bool TryGetHeaderTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null ||
            !httpContext.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeader))
        {
            return false;
        }

        return Guid.TryParse(tenantHeader.FirstOrDefault(), out tenantId);
    }

    private static GovernedAuditAppendRequestBody ToGatewayRequest(HcmAuditAppendRequest request)
        => new()
        {
            CorrelationId = request.CorrelationId,
            RequestType = request.RequestType,
            ActorType = request.ActorType,
            ActorId = request.ActorId,
            TargetTenantId = request.TargetTenantId,
            Category = request.Category,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Operation = request.Operation,
            Outcome = request.Outcome,
            Metadata = request.Metadata,
            OccurredAtUtc = request.OccurredAtUtc,
            SourceService = request.SourceService,
            SourceModule = request.SourceModule
        };

    private static async Task<GovernedAuditAppendResponseBody> ReadAppendResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return new GovernedAuditAppendResponseBody();
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return new GovernedAuditAppendResponseBody
            {
                Status = TryFindString(document.RootElement, "status"),
                AuthoritativePersistenceAccepted =
                    TryFindBool(document.RootElement, "authoritativePersistenceAccepted")
                    ?? TryFindBool(document.RootElement, "AuthoritativePersistenceAccepted")
                    ?? false
            };
        }
        catch (JsonException)
        {
            return new GovernedAuditAppendResponseBody();
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

    private static bool? TryFindBool(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                (property.Value.ValueKind == JsonValueKind.True ||
                 property.Value.ValueKind == JsonValueKind.False))
            {
                return property.Value.GetBoolean();
            }

            var nested = TryFindBool(property.Value, key);
            if (nested.HasValue)
            {
                return nested;
            }
        }

        return null;
    }

    private sealed record GovernedAuditAppendRequestBody
    {
        public Guid CorrelationId { get; init; }
        public string RequestType { get; init; } = string.Empty;
        public string ActorType { get; init; } = "TenantUser";
        public Guid? ActorId { get; init; }
        public Guid TargetTenantId { get; init; }
        public string Category { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public Guid? EntityId { get; init; }
        public string Operation { get; init; } = string.Empty;
        public string Outcome { get; init; } = "Succeeded";
        public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
        public DateTimeOffset OccurredAtUtc { get; init; }
        public string SourceService { get; init; } = string.Empty;
        public string? SourceModule { get; init; }
    }

    private sealed record GovernedAuditAppendResponseBody
    {
        public string? Status { get; init; }
        public bool AuthoritativePersistenceAccepted { get; init; }
    }
}
