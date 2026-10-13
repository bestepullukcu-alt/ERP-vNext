using System.Globalization;
using System.Net;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Trace;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Recall;

/// <summary>
/// Pack §8.6 — the MVP-6 shipment read: <c>GET /api/shipment-bundle/shipments/{id}</c> (SHIPMENT-BUNDLE <c>getShipment</c>,
/// <c>shipment-bundle.openapi.yaml:119-122</c>) through the Gateway, with the CALLER'S token (<c>ForwardCallerHeadersHandler</c>) and the
/// caller's company (<c>X-Legal-Entity-Id</c>). NO cache: every trace reads again. 2 s per read, at most 8 reads in flight. Every outcome
/// is an answer — 200 → Ok (status, actualDeliverAt, shipToReference; the lines are NOT read: they carry no lot), 401 / 403 → Forbidden,
/// 404 → NotFound, timeout / 5xx / no answer → Unavailable — so the trace always completes with the ledger data.
/// </summary>
public sealed class ShipmentBundleHttpReader : IShipmentReader
{
    public const int MaxParallel = 8;
    public static readonly TimeSpan PerReadTimeout = TimeSpan.FromSeconds(2);

    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ShipmentBundleHttpReader> _logger;

    public ShipmentBundleHttpReader(HttpClient http, ITenantContext tenant, ILogger<ShipmentBundleHttpReader> logger)
    {
        _http = http;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, TraceShipmentInfo>> ReadAsync(IReadOnlyCollection<string> shipmentIds, CancellationToken cancellationToken = default)
    {
        using var gate = new SemaphoreSlim(MaxParallel, MaxParallel);
        var reads = shipmentIds.Distinct(StringComparer.Ordinal).Select(async id =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return (id, await ReadOneAsync(id, cancellationToken));
            }
            finally
            {
                gate.Release();
            }
        });
        return (await Task.WhenAll(reads)).ToDictionary(r => r.id, r => r.Item2, StringComparer.Ordinal);
    }

    private async Task<TraceShipmentInfo> ReadOneAsync(string shipmentId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(shipmentId, "D", out var id)) return new TraceShipmentInfo(ShipmentReadOutcome.NotFound, null, null, null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PerReadTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/shipment-bundle/shipments/{id:D}");
            request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", _tenant.LegalEntityId.ToString("D"));
            using var response = await _http.SendAsync(request, timeout.Token);
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token)))
                    {
                        var root = document.RootElement;
                        return new TraceShipmentInfo(ShipmentReadOutcome.Ok, Text(root, "status"),
                            Text(root, "actualDeliverAt") is { } at && DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when) ? when : null,
                            Text(root, "shipToReference"));
                    }
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    return new TraceShipmentInfo(ShipmentReadOutcome.Forbidden, null, null, null);
                case HttpStatusCode.NotFound:
                    return new TraceShipmentInfo(ShipmentReadOutcome.NotFound, null, null, null);
                default:
                    return new TraceShipmentInfo(ShipmentReadOutcome.Unavailable, null, null, null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("recall.shipment.read_unavailable {ShipmentId}: {Reason}", shipmentId, ex.GetType().Name);
            return new TraceShipmentInfo(ShipmentReadOutcome.Unavailable, null, null, null);
        }
    }

    private static string? Text(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
