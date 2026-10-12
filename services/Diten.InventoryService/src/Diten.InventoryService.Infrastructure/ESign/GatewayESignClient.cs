using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.ESign;

/// <summary>
/// MOD-0175 Q3 — MOD-0022 over the Gateway (<c>/api/v1/esign/*</c>), the <see cref="Workflow.GatewayWorkflowClient"/> rule: the
/// CALLER's <c>Authorization</c> + correlation id through <c>ForwardCallerHeadersHandler</c>, <c>X-Tenant-Id</c> written here
/// from the request scope. Platform envelope <c>{ data, errors, reason_code }</c>. 401/403 → forbidden, 404 → not found, 5xx /
/// transport / unreadable → unavailable.
/// </summary>
public sealed class GatewayESignClient : IQuarantineESignClient
{
    private const string TenantHeader = "X-Tenant-Id";

    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<GatewayESignClient> _logger;

    public GatewayESignClient(HttpClient http, ITenantContext tenant, ILogger<GatewayESignClient> logger)
    {
        _http = http;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<ESignCallResult<ESignEffectivePolicy>> EffectivePolicyAsync(string actionCode, Guid decisionId, CancellationToken cancellationToken = default)
    {
        var path = "/api/v1/esign/policy/effective"
                   + $"?actionCode={Uri.EscapeDataString(actionCode)}&module={QuarantineSignableActions.Module}"
                   + $"&subjectType={Uri.EscapeDataString(QuarantineSignableActions.SubjectType)}&subjectId={decisionId:D}";
        var (outcome, data, reason) = await GetAsync(path, cancellationToken);
        if (outcome != WorkflowCallOutcome.Ok)
        {
            return new(outcome, null, reason);
        }

        var required = data!.Value.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.True;
        return new(WorkflowCallOutcome.Ok, new ESignEffectivePolicy(required, Text(data.Value, "meaningCode")), null);
    }

    public async Task<ESignCallResult<ESignSignatureInfo>> SignatureAsync(string signatureId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(signatureId, out var id))
        {
            return new(WorkflowCallOutcome.NotFound, null, null);
        }

        var (outcome, data, reason) = await GetAsync($"/api/v1/esign/signatures/{id:D}", cancellationToken);
        if (outcome != WorkflowCallOutcome.Ok)
        {
            return new(outcome, null, reason);
        }

        var d = data!.Value;
        var subject = d.TryGetProperty("subject", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
        var signer = d.TryGetProperty("signer", out var g) && g.ValueKind == JsonValueKind.Object ? g : default;
        var invalidated = d.TryGetProperty("invalidation", out var inv) && inv.ValueKind == JsonValueKind.Object;
        return new(WorkflowCallOutcome.Ok, new ESignSignatureInfo(
            Text(d, "id") ?? id.ToString("D"),
            signer.ValueKind == JsonValueKind.Object ? Text(signer, "userId") : null,
            subject.ValueKind == JsonValueKind.Object ? Text(subject, "module") : null,
            subject.ValueKind == JsonValueKind.Object ? Text(subject, "subjectType") : null,
            subject.ValueKind == JsonValueKind.Object ? Text(subject, "subjectId") : null,
            Text(d, "meaningCode"),
            Text(d, "subjectHash"),
            invalidated), null);
    }

    private async Task<(WorkflowCallOutcome Outcome, JsonElement? Data, string? Reason)> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (_tenant.IsResolved)
            {
                request.Headers.TryAddWithoutValidation(TenantHeader, _tenant.TenantId.ToString("D"));
            }

            using var response = await _http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(ct);
            JsonElement? data = null;
            string? reason = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d.Clone() : null;
                    reason = Text(root, "reason_code") ?? Text(root, "reasonCode");
                }
            }

            var outcome = status switch
            {
                >= 200 and < 300 when data is not null => WorkflowCallOutcome.Ok,
                401 or 403 => WorkflowCallOutcome.Forbidden,
                404 => WorkflowCallOutcome.NotFound,
                >= 400 and < 500 => WorkflowCallOutcome.Rejected,
                _ => WorkflowCallOutcome.Unavailable
            };
            return (outcome, data, reason);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "quarantine.esign.call_failed Path={Path}", path.Split('?')[0]);
            return (WorkflowCallOutcome.Unavailable, null, null);
        }
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
