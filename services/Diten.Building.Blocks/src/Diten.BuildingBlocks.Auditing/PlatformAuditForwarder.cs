using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.Auditing;

/// <summary>
/// AUD-001 K4 — the ONE HTTP forwarder of path <c>b</c>: <c>POST {BaseUrl}/api/internal/audit/append</c> with
/// <c>X-Internal-Api-Key</c> (Platform's <c>InternalAuditController</c>; the body is its <c>AuditAppendRequest</c>, enums
/// as their integer values). Tenant, actor and correlation come from <see cref="IAuditCallContext"/>.
/// <para>
/// K8 — never silent: unconfigured (address / key / source service unset) or no tenant → a WARNING naming the request
/// and what is missing, and a <c>skipped</c> count; a refusal or an unreachable Platform → a warning and a <c>failed</c>
/// count. Nothing is ever thrown into the business command. The key is never logged.
/// </para>
/// </summary>
public sealed class PlatformAuditForwarder : IPlatformAuditForwarder
{
    public const string HttpClientName = "Diten.BuildingBlocks.Auditing.Platform";
    public const string AppendPath = "/api/internal/audit/append";
    public const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<PlatformAuditForwarderOptions> _options;
    private readonly IAuditCallContext _callContext;
    private readonly AuditForwardingCounters _counters;
    private readonly TimeProvider _clock;
    private readonly ILogger<PlatformAuditForwarder> _logger;

    public PlatformAuditForwarder(
        IHttpClientFactory httpClientFactory,
        IOptions<PlatformAuditForwarderOptions> options,
        IAuditCallContext callContext,
        AuditForwardingCounters counters,
        ILogger<PlatformAuditForwarder> logger,
        TimeProvider? clock = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _callContext = callContext;
        _counters = counters;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<AuditForwardResult> ForwardAsync(AuditForwardRequest request, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var missing = options.MissingSettings();
        if (missing.Count > 0)
        {
            _counters.RecordSkipped("not-configured");
            _logger.LogWarning(
                "AUD-001: audit record NOT written for {RequestType} ({EntityType}): central audit forwarding is not configured (missing {MissingSettings}).",
                request.RequestType, request.EntityType, string.Join(", ", missing));
            return AuditForwardResult.Skipped;
        }

        var caller = _callContext.Resolve();
        if (caller.TenantId is not { } tenantId)
        {
            _counters.RecordSkipped("no-tenant");
            _logger.LogWarning(
                "AUD-001: audit record NOT written for {RequestType} ({EntityType}): the call has no tenant.",
                request.RequestType, request.EntityType);
            return AuditForwardResult.Skipped;
        }

        var payload = new Dictionary<string, object?>
        {
            ["correlationId"] = caller.CorrelationId ?? Guid.NewGuid(),
            ["requestType"] = request.RequestType,
            ["actorType"] = (int)caller.ActorType,
            ["actorId"] = caller.ActorId,
            ["actorEmail"] = caller.ActorEmail,
            ["actorDisplayName"] = caller.ActorDisplayName,
            ["targetTenantId"] = tenantId,
            ["category"] = (int)request.Category,
            ["entityType"] = request.EntityType,
            ["entityId"] = request.EntityId,
            ["operation"] = (int)request.Operation,
            ["outcome"] = (int)request.Outcome,
            ["sourceService"] = options.SourceService,
            ["sourceModule"] = request.SourceModule,
            ["isPlatformGlobal"] = false,
            ["occurredAtUtc"] = _clock.GetUtcNow()
        };
        if (request.BeforeState is not null) payload["beforeState"] = request.BeforeState;
        if (request.AfterState is not null) payload["afterState"] = request.AfterState;
        if (request.Metadata is not null) payload["metadata"] = request.Metadata;

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}{AppendPath}")
            {
                Content = JsonContent.Create(payload)
            };
            message.Headers.TryAddWithoutValidation(InternalApiKeyHeader, options.InternalApiKey);

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _counters.RecordSent();
                return AuditForwardResult.Sent;
            }

            _counters.RecordFailed();
            _logger.LogWarning(
                "AUD-001: audit record NOT written for {RequestType} ({EntityType}): Platform answered {StatusCode}.",
                request.RequestType, request.EntityType, (int)response.StatusCode);
            return AuditForwardResult.Failed;
        }
        catch (Exception ex)
        {
            _counters.RecordFailed();
            _logger.LogWarning(ex,
                "AUD-001: audit record NOT written for {RequestType} ({EntityType}): Platform could not be reached.",
                request.RequestType, request.EntityType);
            return AuditForwardResult.Failed;
        }
    }
}
