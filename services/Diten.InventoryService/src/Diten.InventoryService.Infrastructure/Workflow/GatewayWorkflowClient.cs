using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Workflow;

/// <summary>Configuration of the Gateway the Workflow calls go through (section <c>Gateway</c>, key <c>BaseUrl</c>).</summary>
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string BaseUrl { get; set; } = "http://localhost:5000";

    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// MOD-0175 Q2 — MOD-0023 over the Gateway (<c>/api/v1/workflow/*</c>), the CRM <c>GatewayClaimWorkflowClient</c> pattern: the
/// CALLER's <c>Authorization</c> (and correlation id) travel through <c>ForwardCallerHeadersHandler</c>; the resolved tenant is
/// written here as <c>X-Tenant-Id</c> from the request's own scope (a DelegatingHandler cannot see the request scope — the
/// Platform BL-316 lesson). No service token exists on this path, by design. Platform answers with its <c>Response&lt;T&gt;</c>
/// envelope (<c>data</c>, <c>errors</c>, <c>reason_code</c>).
/// Start outcome map: 404 / a template reason / "no candidates" → template missing; 401/403 → forbidden; 5xx, timeout,
/// network → unavailable; any other 4xx → rejected.
/// </summary>
public sealed class GatewayWorkflowClient : IQuarantineWorkflowClient
{
    private const string TenantHeader = "X-Tenant-Id";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> TemplateMissingReasons = new(StringComparer.Ordinal)
    {
        "NOT_FOUND_NON_LEAKAGE", "WORKFLOW_TEMPLATE_NOT_FOUND", "WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION", "WORKFLOW_TEMPLATE_VERSION_NOT_FOUND",
        "WORKFLOW_TEMPLATE_NOT_PUBLISHED", "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED"
    };

    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<GatewayWorkflowClient> _logger;

    public GatewayWorkflowClient(HttpClient http, ITenantContext tenant, ILogger<GatewayWorkflowClient> logger)
    {
        _http = http;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<WorkflowStartResult> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            templateId = (Guid?)null,
            templateCode = request.TemplateCode,
            objectType = request.ObjectType,
            objectId = request.ObjectId,
            objectRef = request.ObjectRef,
            candidatePrincipalIds = request.CandidatePrincipalIds,
            reasonCode = request.ReasonCode,
            idempotencyKey = request.IdempotencyKey,
            commentRequired = false,
            evidenceRequired = false,
            dueAt = request.DueAt,
            displayContext = new
            {
                title = request.DisplayContext.Title,
                subtitle = request.DisplayContext.Subtitle,
                sourceModule = request.DisplayContext.SourceModule,
                deepLinkUrl = request.DisplayContext.DeepLinkUrl,
                chips = request.DisplayContext.Chips
            }
        };

        var reply = await SendAsync(HttpMethod.Post, "/api/v1/workflow/instances", body, cancellationToken);
        if (reply.Transport is not null)
        {
            return new WorkflowStartResult(WorkflowCallOutcome.Unavailable, null, null, reply.Transport);
        }

        if (reply.Status is >= 200 and < 300 && reply.Data is { ValueKind: JsonValueKind.Object } data
            && Text(data, "workflowInstanceId") is { } instanceId && Guid.TryParse(instanceId, out _))
        {
            return new WorkflowStartResult(WorkflowCallOutcome.Ok, instanceId, null, null);
        }

        var outcome = reply.Status switch
        {
            401 or 403 => WorkflowCallOutcome.Forbidden,
            404 => WorkflowCallOutcome.TemplateMissing,
            >= 500 => WorkflowCallOutcome.Unavailable,
            _ when reply.ReasonCode is not null && TemplateMissingReasons.Contains(reply.ReasonCode) => WorkflowCallOutcome.TemplateMissing,
            >= 200 and < 300 => WorkflowCallOutcome.Unavailable, // a 2xx without an instance id is not an answer
            _ => WorkflowCallOutcome.Rejected
        };
        return new WorkflowStartResult(outcome, null, reply.ReasonCode, reply.Message);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<WorkflowInstanceState>>?> InstancesByObjectsAsync(
        string objectType, IReadOnlyList<string> objectIds, CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/workflow/instances/by-objects?objectType={Uri.EscapeDataString(objectType)}&objectIds={Uri.EscapeDataString(string.Join(',', objectIds))}";
        var reply = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        if (reply.Transport is not null || reply.Status is < 200 or >= 300 || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyList<WorkflowInstanceState>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.EnumerateArray())
        {
            if (Text(row, "objectId") is not { } objectId || !row.TryGetProperty("instances", out var instances) || instances.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            result[objectId] = instances.EnumerateArray()
                .Where(i => Text(i, "workflowInstanceId") is not null)
                .Select(i => new WorkflowInstanceState(Text(i, "workflowInstanceId")!, Text(i, "status") ?? string.Empty, Text(i, "outcome"), Date(i, "completedAt")))
                .ToList();
        }

        return result;
    }

    public async Task<IReadOnlyList<WorkflowHistoryRow>?> HistoryAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var reply = await SendAsync(HttpMethod.Get, $"/api/v1/workflow/instances/{Uri.EscapeDataString(instanceId)}/history", null, cancellationToken);
        if (reply.Transport is not null || reply.Status is < 200 or >= 300 || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return null;
        }

        return rows.EnumerateArray()
            .Select(r => new WorkflowHistoryRow(
                r.TryGetProperty("sequenceNo", out var s) && s.TryGetInt64(out var n) ? n : 0,
                Text(r, "action") ?? string.Empty, Text(r, "actorId"), Date(r, "occurredAt") ?? DateTimeOffset.MinValue, Text(r, "signatureId")))
            .ToList();
    }

    public async Task<IReadOnlyList<WorkflowTaskState>?> TasksAsync(CancellationToken cancellationToken = default)
    {
        var reply = await SendAsync(HttpMethod.Get, "/api/v1/workflow/tasks", null, cancellationToken);
        if (reply.Transport is not null || reply.Status is < 200 or >= 300 || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return null;
        }

        return rows.EnumerateArray()
            .Where(r => Text(r, "id") is not null && Text(r, "workflowInstanceId") is not null)
            .Select(r => new WorkflowTaskState(Text(r, "id")!, Text(r, "workflowInstanceId")!, Text(r, "status") ?? string.Empty))
            .ToList();
    }

    public async Task<WorkflowCallResult<bool>> CancelTaskAsync(
        string taskId, string reasonCode, string idempotencyKey, string? comment, CancellationToken cancellationToken = default)
    {
        // No actorId: MOD-0023 binds the actor to the authenticated caller (WP-0023-FX).
        var reply = await SendAsync(HttpMethod.Post, $"/api/v1/workflow/tasks/{Uri.EscapeDataString(taskId)}/cancel",
            new { reasonCode, idempotencyKey, comment }, cancellationToken);
        return Result(reply, reply.Status is >= 200 and < 300);
    }

    public async Task<WorkflowCallResult<WorkflowDefinitionInfo>> CreateDefinitionAsync(
        string templateCode, string name, string description, CancellationToken cancellationToken = default)
    {
        var reply = await SendAsync(HttpMethod.Post, "/api/v1/workflow/definitions", new { templateCode, name, description }, cancellationToken);
        return Result(reply, reply.Data is { ValueKind: JsonValueKind.Object } d ? Definition(d) : null);
    }

    public async Task<WorkflowCallResult<IReadOnlyList<WorkflowDefinitionInfo>>> DefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var reply = await SendAsync(HttpMethod.Get, "/api/v1/workflow/definitions", null, cancellationToken);
        IReadOnlyList<WorkflowDefinitionInfo>? list = reply.Data is { ValueKind: JsonValueKind.Array } rows
            ? rows.EnumerateArray().Select(Definition).OfType<WorkflowDefinitionInfo>().ToList()
            : null;
        if (list is not null && reply.Status is >= 200 and < 300)
        {
            // the list item carries no active version: read each matching definition's detail
            var detailed = new List<WorkflowDefinitionInfo>();
            foreach (var item in list)
            {
                var detail = await SendAsync(HttpMethod.Get, $"/api/v1/workflow/definitions/{Uri.EscapeDataString(item.TemplateId)}", null, cancellationToken);
                detailed.Add(detail.Data is { ValueKind: JsonValueKind.Object } d && Definition(d) is { } full ? full : item);
            }

            list = detailed;
        }

        return Result(reply, list);
    }

    public async Task<WorkflowCallResult<int>> VersionNumberAsync(string templateId, string versionId, CancellationToken cancellationToken = default)
    {
        var reply = await SendAsync(HttpMethod.Get,
            $"/api/v1/workflow/definitions/{Uri.EscapeDataString(templateId)}/versions/{Uri.EscapeDataString(versionId)}", null, cancellationToken);
        var number = reply.Data is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("versionNumber", out var v) && v.TryGetInt32(out var n) ? n : 0;
        return Result(reply, number);
    }

    public async Task<WorkflowCallResult<int>> PublishAsync(
        string templateId, string definitionJson, string schemaVersion, string expressionVersion, string reason, CancellationToken cancellationToken = default)
    {
        var reply = await SendAsync(HttpMethod.Post, $"/api/v1/workflow/definitions/{Uri.EscapeDataString(templateId)}/publish",
            new { definitionJson, schemaVersion, expressionVersion, expectedTemplateVersion = (int?)null, expectedRowVersion = (string?)null, publishReason = reason },
            cancellationToken);
        var number = reply.Data is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("versionNumber", out var v) && v.TryGetInt32(out var n) ? n : 0;
        return Result(reply, number);
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    private static WorkflowDefinitionInfo? Definition(JsonElement d)
        => Text(d, "id") is { } id && Text(d, "templateCode") is { } code
            ? new WorkflowDefinitionInfo(id, code, Text(d, "status") ?? string.Empty, Text(d, "activePublishedVersionId"))
            : null;

    private static WorkflowCallResult<T> Result<T>(Reply reply, T? value)
    {
        if (reply.Transport is not null)
        {
            return new WorkflowCallResult<T>(WorkflowCallOutcome.Unavailable, default, null, reply.Transport);
        }

        var outcome = reply.Status switch
        {
            >= 200 and < 300 => WorkflowCallOutcome.Ok,
            401 or 403 => WorkflowCallOutcome.Forbidden,
            404 => WorkflowCallOutcome.NotFound,
            409 => WorkflowCallOutcome.Conflict,
            >= 500 => WorkflowCallOutcome.Unavailable,
            _ => WorkflowCallOutcome.Rejected
        };
        return new WorkflowCallResult<T>(outcome, outcome == WorkflowCallOutcome.Ok ? value : default, reply.ReasonCode, reply.Message);
    }

    private sealed record Reply(int Status, JsonElement? Data, string? ReasonCode, string? Message, string? Transport);

    private async Task<Reply> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            if (_tenant.IsResolved)
            {
                request.Headers.TryAddWithoutValidation(TenantHeader, _tenant.TenantId.ToString("D"));
            }

            using var response = await _http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Reply(status, null, null, null, null);
            }

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            JsonElement? data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d.Clone() : null;
            var reason = root.ValueKind == JsonValueKind.Object ? Text(root, "reason_code") ?? Text(root, "reasonCode") : null;
            string? message = null;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                message = string.Join("; ", errors.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()));
            }

            return new Reply(status, data, reason, message, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "quarantine.workflow.call_failed Method={Method} Path={Path}", method, path);
            return new Reply(503, null, null, null, ex.GetType().Name);
        }
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        } : null;

    private static DateTimeOffset? Date(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var parsed) ? parsed : null;
}
