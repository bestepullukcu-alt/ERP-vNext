using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Domain.Entities.Commercial;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168's MDM reads through the Gateway with the caller's token (fail closed):
/// <list type="bullet">
/// <item><c>GET api/legal-entities/{id}</c> — the seller's code, name, country, base currency (404 → not found, else
/// unavailable);</item>
/// <item><c>POST api/product-master/skus/bulk-read</c> (≤ 200 ids) — status, code, name, level; then <c>GET
/// skus/{id}</c> for each ACTIVE SKU (pack quantity / unit, GTIN). A failed detail read leaves those fields empty (display
/// data); a failed bulk read is "could not ask" (null).</item>
/// </list>
/// </summary>
public sealed class MdmPreOrderDirectory : IPreOrderMdmDirectory
{
    private const string ProductMaster = "api/product-master";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly ITenantContext _tenant;
    private readonly ILogger<MdmPreOrderDirectory> _logger;

    public MdmPreOrderDirectory(HttpClient http, IConfiguration configuration, IHttpContextAccessor context, ITenantContext tenant,
        ILogger<MdmPreOrderDirectory> logger)
    {
        _http = http;
        _context = context;
        _tenant = tenant;
        _logger = logger;
        var gateway = configuration["Gateway:BaseUrl"] ?? configuration["GatewayUrl"] ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
    }

    public async Task<(PreOrderLookupOutcome Outcome, PreOrderLegalEntity? LegalEntity)> GetLegalEntityAsync(Guid legalEntityId, CancellationToken ct)
    {
        if (_tenant.TenantId is null || legalEntityId == Guid.Empty)
        {
            return (PreOrderLookupOutcome.NotFound, null);
        }

        var (status, data) = await SendAsync(HttpMethod.Get, "api/legal-entities/" + legalEntityId.ToString("D"), null, ct);
        if (status == HttpStatusCode.NotFound)
        {
            return (PreOrderLookupOutcome.NotFound, null);
        }

        if (data is not { ValueKind: JsonValueKind.Object } le || Text(le, "countryCode") is not { } country)
        {
            return (PreOrderLookupOutcome.Unavailable, null);
        }

        var code = Text(le, "code") ?? string.Empty;
        return (PreOrderLookupOutcome.Found, new PreOrderLegalEntity(legalEntityId, code, Text(le, "displayName") ?? Text(le, "legalName") ?? code,
            country.ToUpperInvariant(), (Text(le, "baseCurrencyCode") ?? string.Empty).ToUpperInvariant()));
    }

    public async Task<IReadOnlyDictionary<Guid, PreOrderSku>?> ReadSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, PreOrderSku>();
        foreach (var chunk in skuIds.Where(id => id != Guid.Empty).Distinct().Chunk(200))
        {
            var (_, data) = await SendAsync(HttpMethod.Post, ProductMaster + "/skus/bulk-read", new { skuIds = chunk }, ct);
            if (data is not { ValueKind: JsonValueKind.Object } body || !body.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var row in items.EnumerateArray())
            {
                if (Text(row, "skuId") is { } raw && Guid.TryParse(raw, out var id))
                {
                    result[id] = new PreOrderSku(id, Text(row, "skuLevel") ?? string.Empty, Text(row, "canonicalCode") ?? string.Empty,
                        Text(row, "name") ?? string.Empty, Text(row, "lifecycleStatus") ?? string.Empty);
                }
            }
        }

        var details = await Task.WhenAll(result.Values.Where(s => s.IsActive).Select(async sku =>
        {
            var (_, data) = await SendAsync(HttpMethod.Get, ProductMaster + "/skus/" + sku.SkuId.ToString("D"), null, ct);
            return data is { ValueKind: JsonValueKind.Object } d
                ? sku with
                {
                    PackQuantity = decimal.TryParse(Text(d, "packQuantity"), NumberStyles.Number, CultureInfo.InvariantCulture, out var q) ? q : null,
                    PackUomCode = Text(d, "packUomCode"), Gtin = Text(d, "gtin")
                }
                : sku;
        }));
        foreach (var sku in details)
        {
            result[sku.SkuId] = sku;
        }

        return result;
    }

    private async Task<(HttpStatusCode? Status, JsonElement? Data)> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (_tenant.TenantId is null)
        {
            return (null, null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            MdmForwarding.Forward(request, _context, _tenant);
            using var response = await _http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("pre_order.mdm.read_failed Status={Status} Path={Path}", (int)response.StatusCode, path.Split('/')[1]);
                return (response.StatusCode, null);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = document.RootElement;
            return (response.StatusCode, (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) ? data : root).Clone());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "pre_order.mdm.unavailable");
            return (null, null);
        }
    }

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? (v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString()?.Trim()) is { Length: > 0 } t ? t : null
            : null;
}

/// <summary>
/// WP-VW-W6 W6-BE-a — Platform PLT-W6 <c>GET api/platform/organization/seats/{userId}/line-managers</c> through the
/// Gateway with the caller's token (the W3L seat client pattern: 3 s budget, one retry on 502/503/504, never throws).
/// Platform answers the rep's own managers without a key; another user's needs <c>platform.organization-seats.read</c>.
/// </summary>
public sealed class PlatformPreOrderLineManagerReader : IPreOrderLineManagerReader
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly ITenantContext _tenant;
    private readonly string _basePath;

    public PlatformPreOrderLineManagerReader(HttpClient http, IConfiguration configuration, IHttpContextAccessor context, ITenantContext tenant)
    {
        _http = http;
        _context = context;
        _tenant = tenant;
        var gateway = configuration["Gateway:BaseUrl"] ?? configuration["GatewayUrl"] ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
        _basePath = (configuration["VisitEvidence:OrganizationSeatsPath"] ?? "api/platform/organization/seats").TrimEnd('/');
    }

    public async Task<PreOrderLineManagers> GetAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            return PreOrderLineManagers.Unavailable;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TotalTimeout);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{_basePath}/{userId:D}/line-managers");
                MdmForwarding.Forward(request, _context, _tenant);
                using var response = await _http.SendAsync(request, timeout.Token);
                if (response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout && attempt == 0)
                {
                    await Task.Delay(RetryDelay, timeout.Token);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return PreOrderLineManagers.Unavailable;
                }

                var envelope = await response.Content.ReadFromJsonAsync<Envelope>(cancellationToken: timeout.Token);
                if (envelope?.IsSuccessful != true || envelope.Data is not { } dto)
                {
                    return PreOrderLineManagers.Unavailable;
                }

                return new PreOrderLineManagers(true, dto.Resolution ?? "None", dto.PositionId, dto.PositionCode, dto.PositionName,
                    (dto.Managers ?? []).Where(m => m is not null && m.UserId != Guid.Empty && m.UserId != userId)
                    .Select(m => new PreOrderLineManager(m.UserId, m.DisplayName, m.PositionId, m.PositionCode)).ToList());
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return PreOrderLineManagers.Unavailable;
        }

        return PreOrderLineManagers.Unavailable;
    }

    private sealed record Envelope(LineManagersBody? Data, bool IsSuccessful);

    private sealed record ManagerBody(Guid UserId, string? DisplayName, Guid PositionId, string? PositionCode);

    private sealed record LineManagersBody(Guid UserId, string? Resolution, Guid? PositionId, string? PositionCode, string? PositionName,
        IReadOnlyList<ManagerBody>? Managers);
}

/// <summary>
/// WP-VW-W6 W6-BE-a — the e-mail events until W6-BE-c wires Platform Notifications: one log line each (no address, no
/// token). Never throws (K-N8).
/// </summary>
public sealed class LoggingPreOrderNotifier : IPreOrderNotifier
{
    private readonly ILogger<LoggingPreOrderNotifier> _logger;

    public LoggingPreOrderNotifier(ILogger<LoggingPreOrderNotifier> logger) => _logger = logger;

    public Task ApprovalRequestedAsync(PreOrder order, string approvalToken, DateTimeOffset expiresAt, CancellationToken ct)
        => Log("approval-requested", order);

    public Task AutoApprovedAsync(PreOrder order, CancellationToken ct) => Log("auto-approved", order);

    public Task ChangesRequestedAsync(PreOrder order, string reason, CancellationToken ct) => Log("changes-requested", order);

    public Task RejectedAsync(PreOrder order, string reason, CancellationToken ct) => Log("rejected", order);

    public Task CancelledAsync(PreOrder order, string reason, CancellationToken ct) => Log("cancelled", order);

    private Task Log(string eventCode, PreOrder order)
    {
        _logger.LogInformation("pre_order.notify.pending_w6_bec Event=commercial.pre-order.{Event} PreOrderId={Id} Code={Code}", eventCode, order.Id, order.Code);
        return Task.CompletedTask;
    }
}

/// <summary>Until W6-BE-c: nothing is sent, the approved order stays <c>approved</c> (never a fake "transmitted").</summary>
public sealed class NotWiredPreOrderTransmitter : IPreOrderTransmitter
{
    public Task<PreOrderTransmissionResult> TransmitAsync(PreOrder order, int attemptNo, CancellationToken ct)
        => Task.FromResult(PreOrderTransmissionResult.NotWired);
}

/// <summary><c>Commercial:PreOrders:Workflow:TemplateCode</c> (default <c>COM-PREORDER-APPROVAL</c>) and
/// <c>…:ReconcileAfterSeconds</c> (default 120).</summary>
public sealed class ConfigurationPreOrderApprovalSettings : IPreOrderApprovalSettings
{
    public ConfigurationPreOrderApprovalSettings(IConfiguration configuration)
    {
        var template = configuration["Commercial:PreOrders:Workflow:TemplateCode"];
        TemplateCode = string.IsNullOrWhiteSpace(template) ? PreOrderApprovalDefaults.TemplateCode : template.Trim();
        ReconcileAfterSeconds = int.TryParse(configuration["Commercial:PreOrders:Workflow:ReconcileAfterSeconds"], out var seconds) && seconds > 0
            ? seconds
            : PreOrderApprovalDefaults.ReconcileAfterSeconds;
    }

    public string TemplateCode { get; }
    public int ReconcileAfterSeconds { get; }
}
