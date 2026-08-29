using System.Net.Http.Json;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Contact;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Audit;

/// <summary>
/// HTTP-ready MOD-0021 audit seam for CRM. Forwards Account/Contact audit events to the governed audit append
/// contract (<c>POST /api/v1/platform/audit/events</c>) over the Gateway, mirroring the HcmService client.
/// <para>
/// Fail-soft: an unavailable/erroring audit dependency is logged and swallowed — it never breaks the business
/// operation (audit is a side effect, not a gate). PII-safe: only the caller-supplied <c>detail</c> string
/// (counts + correlation id, never row payload or credentials) is carried as metadata.
/// </para>
/// Opt-in: registered only when <c>Crm:Audit:Mode=http</c>; the default logging seam stays otherwise.
/// </summary>
public sealed class HttpCrmAuditPublisher : IContactAuditPublisher, IAccountAuditPublisher
{
    private const string AppendPath = "/api/v1/platform/audit/events";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string AuthorizationHeaderName = "Authorization";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<HttpCrmAuditPublisher> _logger;

    public HttpCrmAuditPublisher(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<HttpCrmAuditPublisher> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5000");
    }

    // IContactAuditPublisher — contactId is the target entity.
    public Task PublishAsync(string eventName, Guid tenantId, Guid contactId, string? detail, CancellationToken cancellationToken)
        => AppendAsync(eventName, tenantId, "Contact", contactId, detail, cancellationToken);

    private async Task AppendAsync(string eventName, Guid tenantId, string entityType, Guid entityId, string? detail, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return;
        }

        var body = new AuditAppendBody
        {
            CorrelationId = Guid.NewGuid(),
            RequestType = eventName,
            ActorType = "TenantUser",
            TargetTenantId = tenantId,
            Category = "Crm",
            EntityType = entityType,
            EntityId = entityId == Guid.Empty ? null : entityId,
            Operation = eventName,
            Outcome = "Succeeded",
            // detail is counts/correlation only (see class remarks) — safe to carry as metadata.
            Metadata = string.IsNullOrWhiteSpace(detail)
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["detail"] = detail },
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = "CrmService",
            SourceModule = "MOD-0150"
        };

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, AppendPath)
            {
                Content = JsonContent.Create(body)
            };
            ForwardContextHeaders(message, body.CorrelationId);

            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CRM audit append for {AuditEvent} returned {Status}; event dropped (fail-soft).", eventName, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "CRM audit append dependency unavailable for {AuditEvent}; event dropped (fail-soft).", eventName);
        }
    }

    // IAccountAuditPublisher — explicit implementation so both interfaces coexist on one type.
    Task IAccountAuditPublisher.PublishAsync(string eventName, Guid tenantId, Guid accountId, string? detail, CancellationToken cancellationToken)
        => AppendAsync(eventName, tenantId, "Account", accountId, detail, cancellationToken);

    private void ForwardContextHeaders(HttpRequestMessage message, Guid correlationId)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            message.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId.ToString("D"));
            return;
        }

        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenant) && !string.IsNullOrWhiteSpace(tenant))
        {
            message.Headers.TryAddWithoutValidation(TenantHeaderName, tenant.ToArray());
        }

        if (context.Request.Headers.TryGetValue(AuthorizationHeaderName, out var auth) && !string.IsNullOrWhiteSpace(auth))
        {
            message.Headers.TryAddWithoutValidation(AuthorizationHeaderName, auth.ToArray());
        }

        if (context.Request.Headers.TryGetValue(CorrelationHeaderName, out var corr) && !string.IsNullOrWhiteSpace(corr))
        {
            message.Headers.TryAddWithoutValidation(CorrelationHeaderName, corr.ToArray());
        }
        else
        {
            message.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId.ToString("D"));
        }
    }

    private sealed record AuditAppendBody
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
}
