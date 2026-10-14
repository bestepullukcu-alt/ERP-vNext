using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.ESign;

/// <summary>
/// MOD-0177 R4 — MOD-0022 over the Gateway (<c>/api/v1/esign/*</c>), COPIED from the MOD-0176 <see cref="GatewayExpiryESignClient"/> (itself the
/// MOD-0175 <see cref="GatewayESignClient"/> rule — copied, not shared, so no other module's client changes): the CALLER's
/// <c>Authorization</c> + correlation id through <c>ForwardCallerHeadersHandler</c>, <c>X-Tenant-Id</c> from the request scope, Platform envelope
/// <c>{ data, errors, reason_code }</c>. Two reads only — <c>getEffectivePolicy</c> (§4.15, module <c>recall</c>) and <c>getSignature</c> (§4.5,
/// <c>esign.openapi.yaml:654-684</c>); never <c>verifySignature</c> (CT decision F3-A). 401/403 → forbidden, 404 → not found, other 4xx →
/// rejected, 5xx / transport / timeout / unreadable → unavailable (callers fail closed on anything but Ok).
/// </summary>
public sealed class GatewayRecallESignClient : IRecallESignClient
{
    private const string TenantHeader = "X-Tenant-Id";

    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<GatewayRecallESignClient> _logger;

    public GatewayRecallESignClient(HttpClient http, ITenantContext tenant, ILogger<GatewayRecallESignClient> logger)
    {
        _http = http;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<RecallESignCall<RecallESignPolicy>> EffectivePolicyAsync(string actionCode, string subjectType, string subjectId,
        CancellationToken cancellationToken = default)
    {
        var path = "/api/v1/esign/policy/effective"
                   + $"?actionCode={Uri.EscapeDataString(actionCode)}&module={RecallSignableActions.Module}"
                   + $"&subjectType={Uri.EscapeDataString(subjectType)}&subjectId={Uri.EscapeDataString(subjectId)}";
        var (outcome, data) = await GetAsync(path, cancellationToken);
        if (outcome != RecallESignOutcome.Ok)
        {
            return new(outcome, null);
        }

        var required = data!.Value.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.True;
        return new(RecallESignOutcome.Ok, new RecallESignPolicy(required, Text(data.Value, "meaningCode"), Text(data.Value, "visualMode")));
    }

    public async Task<RecallESignCall<RecallESignSignature>> SignatureAsync(string signatureId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(signatureId, out var id))
        {
            return new(RecallESignOutcome.NotFound, null);
        }

        var (outcome, data) = await GetAsync($"/api/v1/esign/signatures/{id:D}", cancellationToken);
        if (outcome != RecallESignOutcome.Ok)
        {
            return new(outcome, null);
        }

        var d = data!.Value;
        var subject = d.TryGetProperty("subject", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
        var signer = d.TryGetProperty("signer", out var g) && g.ValueKind == JsonValueKind.Object ? g : default;
        var invalidated = d.TryGetProperty("invalidation", out var inv) && inv.ValueKind == JsonValueKind.Object;
        DateTimeOffset? signedAt = DateTimeOffset.TryParse(Text(d, "signedAtUtc"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : null;
        return new(RecallESignOutcome.Ok, new RecallESignSignature(
            Text(d, "id") ?? id.ToString("D"),
            signer.ValueKind == JsonValueKind.Object ? Text(signer, "userId") : null,
            subject.ValueKind == JsonValueKind.Object ? Text(subject, "module") : null,
            subject.ValueKind == JsonValueKind.Object ? Text(subject, "subjectType") : null,
            subject.ValueKind == JsonValueKind.Object ? Text(subject, "subjectId") : null,
            Text(d, "actionCode"),
            Text(d, "meaningCode"),
            Text(d, "subjectHash"),
            signedAt,
            invalidated));
    }

    private async Task<(RecallESignOutcome Outcome, JsonElement? Data)> GetAsync(string path, CancellationToken ct)
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
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
                {
                    data = d.Clone();
                }
            }

            var outcome = status switch
            {
                >= 200 and < 300 when data is not null => RecallESignOutcome.Ok,
                401 or 403 => RecallESignOutcome.Forbidden,
                404 => RecallESignOutcome.NotFound,
                >= 400 and < 500 => RecallESignOutcome.Rejected,
                _ => RecallESignOutcome.Unavailable
            };
            return (outcome, data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "recall.esign.call_failed Path={Path}", path.Split('?')[0]);
            return (RecallESignOutcome.Unavailable, null);
        }
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
