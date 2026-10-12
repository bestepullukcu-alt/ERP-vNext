using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Workflow;

/// <summary>
/// WP-CL-BE-4 — MOD-0023 over the Gateway (<c>/api/v1/workflow/*</c>, WP-CL-BE-3 contract), forwarding the CALLER's
/// <c>Authorization</c> and <c>X-Tenant-Id</c> exactly like <c>GatewayReferenceDataValidator</c>. There is deliberately
/// no service-token path: the submitter must be the user, or MOD-0023's SoD rule ("the submitter cannot approve") is
/// silently switched off.
/// <para>Outcome mapping of a start: 404 / a template 409 / "no candidates" 400 → template missing; 401/403 →
/// forbidden; 5xx, timeout or network failure → unavailable; any other 4xx → rejected (with MOD-0023's message).</para>
/// <para>WP-KP-2 — the same client answers <see cref="IWorkflowDecisionClient"/> (knowledge path reviewer decisions):
/// <c>tasks/mine</c>, <c>tasks/{id}/approve|reject</c> WITH the caller's comment, and <c>instances/{id}/history</c>.</para>
/// </summary>
public sealed class GatewayClaimWorkflowClient : IClaimWorkflowClient, IWorkflowDecisionClient
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string AuthorizationHeaderName = "Authorization";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> TemplateMissingReasons = new(StringComparer.Ordinal)
    {
        "NOT_FOUND_NON_LEAKAGE",
        "WORKFLOW_TEMPLATE_NOT_FOUND",
        "WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION",
        "WORKFLOW_TEMPLATE_VERSION_NOT_FOUND",
        "WORKFLOW_TEMPLATE_NOT_PUBLISHED",
        "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED"
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GatewayClaimWorkflowClient> _logger;

    public GatewayClaimWorkflowClient(HttpClient http, IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor, ILogger<GatewayClaimWorkflowClient> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _http.BaseAddress ??= new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5000");
    }

    public async Task<ClaimWorkflowStartResult> StartAsync(ClaimWorkflowStartRequest request, CancellationToken ct)
    {
        var body = new
        {
            templateId = (Guid?)null,
            templateCode = request.TemplateCode,
            objectType = request.ObjectType,
            objectId = request.ObjectId,
            objectRef = request.ObjectRef,
            // Candidates come from the template's positions (WP-ORG-02); CRM sends none.
            candidatePrincipalIds = Array.Empty<string>(),
            // WP-KP-5a-FIX-1 — the caller's kind decides the audit label; the claim code is only the default.
            reasonCode = string.IsNullOrWhiteSpace(request.ReasonCode) ? ClaimReviewRules.SubmitReasonCode : request.ReasonCode,
            idempotencyKey = request.IdempotencyKey,
            commentRequired = false,
            evidenceRequired = false,
            dueAt = (DateTimeOffset?)null,
            displayContext = new
            {
                title = request.DisplayContext.Title,
                subtitle = request.DisplayContext.Subtitle,
                sourceModule = request.DisplayContext.SourceModule,
                deepLinkUrl = request.DisplayContext.DeepLinkUrl,
                chips = request.DisplayContext.Chips
            }
        };

        var reply = await SendAsync(HttpMethod.Post, "/api/v1/workflow/instances", body, ct);
        if (reply.Transport is not null)
        {
            return new ClaimWorkflowStartResult(ClaimWorkflowCallOutcome.Unavailable, null, reply.Transport);
        }

        if (reply.Status is >= 200 and < 300
            && reply.Data is { ValueKind: JsonValueKind.Object } data
            && data.TryGetProperty("workflowInstanceId", out var id)
            && id.TryGetGuid(out var instanceId))
        {
            return new ClaimWorkflowStartResult(ClaimWorkflowCallOutcome.Ok, instanceId, null);
        }

        var outcome = reply.Status switch
        {
            401 or 403 => ClaimWorkflowCallOutcome.Forbidden,
            404 => ClaimWorkflowCallOutcome.TemplateMissing,
            >= 500 => ClaimWorkflowCallOutcome.Unavailable,
            _ when reply.ReasonCode is not null && TemplateMissingReasons.Contains(reply.ReasonCode)
                => ClaimWorkflowCallOutcome.TemplateMissing,
            >= 200 and < 300 => ClaimWorkflowCallOutcome.Unavailable, // a 2xx without an instance id is not an answer
            _ => ClaimWorkflowCallOutcome.Rejected
        };
        return new ClaimWorkflowStartResult(outcome, null, reply.Message);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>>?> GetInstancesByObjectsAsync(
        string objectType, IReadOnlyList<string> objectIds, CancellationToken ct)
    {
        var path = $"/api/v1/workflow/instances/by-objects?objectType={Uri.EscapeDataString(objectType)}"
                   + $"&objectIds={Uri.EscapeDataString(string.Join(',', objectIds))}";
        var reply = await SendAsync(HttpMethod.Get, path, null, ct);
        if (reply.Transport is not null || reply.Status is < 200 or >= 300
            || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.EnumerateArray())
        {
            var objectId = Text(row, "objectId");
            if (objectId is null || !row.TryGetProperty("instances", out var instances)
                || instances.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            result[objectId] = instances.EnumerateArray()
                .Where(i => i.TryGetProperty("workflowInstanceId", out var g) && g.TryGetGuid(out _))
                .Select(i => new ClaimWorkflowInstanceState(
                    i.GetProperty("workflowInstanceId").GetGuid(),
                    Text(i, "status") ?? string.Empty,
                    Text(i, "outcome"),
                    Date(i, "completedAt")))
                .ToList();
        }

        return result;
    }

    public async Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetTasksAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct)
    {
        var reply = await SendAsync(HttpMethod.Get, "/api/v1/workflow/tasks", null, ct);
        if (reply.Transport is not null || reply.Status is < 200 or >= 300
            || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return null;
        }

        var wanted = workflowInstanceIds.ToHashSet();
        var tasks = new List<ClaimWorkflowTaskState>();
        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("workflowInstanceId", out var instance) || !instance.TryGetGuid(out var instanceId)
                || !wanted.Contains(instanceId)
                || !row.TryGetProperty("id", out var id) || !id.TryGetGuid(out var taskId))
            {
                continue;
            }

            tasks.Add(new ClaimWorkflowTaskState(taskId, instanceId, Text(row, "stageCode") ?? string.Empty,
                Text(row, "stepCode") ?? string.Empty, Text(row, "status") ?? string.Empty, Text(row, "assigneeRef"),
                Text(row, "actionedBy"), Text(row, "actionReasonCode"), Date(row, "dueAt"), Date(row, "completedAt")));
        }

        return tasks;
    }

    public async Task<ClaimWorkflowCallOutcome> CancelTaskAsync(
        Guid taskId, string actorId, string reasonCode, string idempotencyKey, CancellationToken ct)
    {
        var reply = await SendAsync(HttpMethod.Post, $"/api/v1/workflow/tasks/{taskId:D}/cancel",
            new { actorId, reasonCode, idempotencyKey, comment = (string?)null }, ct);
        return reply.Transport is not null
            ? ClaimWorkflowCallOutcome.Unavailable
            : reply.Status switch
            {
                >= 200 and < 300 => ClaimWorkflowCallOutcome.Ok,
                401 or 403 => ClaimWorkflowCallOutcome.Forbidden,
                404 => ClaimWorkflowCallOutcome.NotFound,
                >= 500 => ClaimWorkflowCallOutcome.Unavailable,
                _ => ClaimWorkflowCallOutcome.Rejected
            };
    }

    // ---------------- WP-KP-2 — IWorkflowDecisionClient ----------------

    public async Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct)
    {
        var reply = await SendAsync(HttpMethod.Get, "/api/v1/workflow/tasks/mine", null, ct);
        if (reply.Transport is not null || reply.Status is < 200 or >= 300
            || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return null;
        }

        var tasks = new List<ClaimWorkflowTaskState>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.TryGetProperty("workflowInstanceId", out var instance) && instance.TryGetGuid(out var instanceId)
                && row.TryGetProperty("id", out var id) && id.TryGetGuid(out var taskId))
            {
                tasks.Add(new ClaimWorkflowTaskState(taskId, instanceId, Text(row, "stageCode") ?? string.Empty,
                    Text(row, "stepCode") ?? string.Empty, Text(row, "status") ?? string.Empty, Text(row, "assigneeRef"),
                    Text(row, "actionedBy"), Text(row, "actionReasonCode"), Date(row, "dueAt"), Date(row, "completedAt")));
            }
        }

        return tasks;
    }

    public async Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode,
        string idempotencyKey, string? comment, CancellationToken ct)
    {
        var reply = await SendAsync(HttpMethod.Post, $"/api/v1/workflow/tasks/{taskId:D}/{(approve ? "approve" : "reject")}",
            new { actorId, reasonCode, idempotencyKey, comment, evidenceRef = (string?)null }, ct);
        if (reply.Transport is not null)
        {
            return new WorkflowDecisionResult(ClaimWorkflowCallOutcome.Unavailable, reply.Transport);
        }

        var outcome = reply.Status switch
        {
            >= 200 and < 300 => ClaimWorkflowCallOutcome.Ok,
            401 or 403 => ClaimWorkflowCallOutcome.Forbidden,
            404 => ClaimWorkflowCallOutcome.NotFound,
            >= 500 => ClaimWorkflowCallOutcome.Unavailable,
            _ => ClaimWorkflowCallOutcome.Rejected
        };
        return new WorkflowDecisionResult(outcome, reply.Message);
    }

    public async Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct)
    {
        var reply = await SendAsync(HttpMethod.Get, $"/api/v1/workflow/instances/{workflowInstanceId:D}/history", null, ct);
        if (reply.Transport is not null || reply.Status is < 200 or >= 300
            || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return null;
        }

        return rows.EnumerateArray()
            .Where(r => r.ValueKind == JsonValueKind.Object)
            .Select(r => new WorkflowHistoryEntry(
                r.TryGetProperty("sequenceNo", out var seq) && seq.TryGetInt64(out var n) ? n : 0,
                Text(r, "action") ?? string.Empty,
                Text(r, "actorId"),
                Text(r, "actorDisplay"),
                Text(r, "fromStepCode") ?? Text(r, "toStepCode"),
                Text(r, "stepName"),
                Text(r, "comment"),
                Text(r, "reasonCode"),
                Date(r, "occurredAt") ?? DateTimeOffset.MinValue))
            .ToList();
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

            ForwardContextHeaders(request);
            using var response = await _http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Reply(status, null, null, null, null);
            }

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            JsonElement? data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d)
                ? d.Clone()
                : null;
            var reason = root.ValueKind == JsonValueKind.Object ? Text(root, "reason_code") ?? Text(root, "reasonCode") : null;
            string? message = null;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                message = string.Join("; ", errors.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()));
            }

            return new Reply(status, data, reason, message, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "claims.workflow.call_failed Method={Method} Path={Path}", method, path);
            return new Reply((int)HttpStatusCode.ServiceUnavailable, null, null, null, ex.GetType().Name);
        }
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenant) && !string.IsNullOrWhiteSpace(tenant))
        {
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenant.ToString());
        }

        if (context.Request.Headers.TryGetValue(AuthorizationHeaderName, out var auth) && !string.IsNullOrWhiteSpace(auth))
        {
            request.Headers.TryAddWithoutValidation(AuthorizationHeaderName, auth.ToString());
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Date(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.TryGetDateTimeOffset(out var parsed)
            ? parsed
            : null;
}
