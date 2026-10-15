using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Microsoft.Extensions.Configuration;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

public sealed class LiveHistoryImportReferences : IHistoryImportScopeAuthority, IHistoryRowReferenceChecker
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly string _unitMode;

    public LiveHistoryImportReferences(IHttpClientFactory factory, IHttpContextAccessor context,
        IConfiguration configuration)
    {
        _http = factory.CreateClient("DemandGateway");
        _context = context;
        _unitMode = configuration["DemandPlanning:HistoryUnitMode"] ??
            throw new InvalidOperationException("DemandPlanning:HistoryUnitMode is required.");
        if (_unitMode is not ("SingleUnitInterim" or "ProductMasterV1"))
            throw new InvalidOperationException("Unsupported DemandPlanning:HistoryUnitMode.");
    }

    public async Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
        Guid selectedLegalEntityHint, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty || selectedLegalEntityHint == Guid.Empty)
            return null;
        var entity = await ReadAsync($"api/legal-entities/{selectedLegalEntityHint:D}/lookup-validation",
            cancellationToken);
        if (entity is null) return null;
        return RequiredGuid(entity.Value, "legalEntityId") == selectedLegalEntityHint &&
               RequiredBool(entity.Value, "referenceable") ? selectedLegalEntityHint : null;
    }

    public async Task<bool?> IsAuthorizedAsync(Guid tenantId, Guid actorId, Guid legalEntityId,
        DateOnly from, DateOnly through, IReadOnlyList<string> warehouses,
        CancellationToken cancellationToken)
    {
        if (warehouses.Count == 0 || from > through) return false;
        foreach (var warehouse in warehouses.Distinct(StringComparer.Ordinal))
        {
            if (!await IsActiveWarehouseAsync(legalEntityId, warehouse, cancellationToken))
                return false;
        }
        return true;
    }

    public async Task<HistoryReferenceResult> CheckAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, string skuLevel, string warehouseId, DateOnly occurredOn,
        string originalUomId, CancellationToken cancellationToken)
    {
        var invalid = new HistoryReferenceResult(HistoryReferenceState.Invalid,
            null, null, null, null);
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || skuId == Guid.Empty ||
            string.IsNullOrWhiteSpace(originalUomId) ||
            !await IsActiveWarehouseAsync(legalEntityId, warehouseId, cancellationToken))
            return invalid;
        if (skuLevel is not ("Gsku" or "Lsku")) return invalid;
        var sku = await ReadAsync($"api/{skuLevel.ToLowerInvariant()}s/{skuId:D}", cancellationToken);
        if (sku is null || RequiredGuid(sku.Value, "id") != skuId ||
            !IsIdentityApproved(sku.Value)) return invalid;
        if (skuLevel == "Lsku")
        {
            var parentId = RequiredGuid(sku.Value, "gskuId");
            var parent = await ReadAsync($"api/gskus/{parentId:D}", cancellationToken);
            if (parent is null || RequiredGuid(parent.Value, "id") != parentId ||
                !IsIdentityApproved(parent.Value)) return invalid;
        }
        if (_unitMode == "SingleUnitInterim")
            return new HistoryReferenceResult(HistoryReferenceState.Verified,
                null, null, null, null, UnitVerificationPending: true);

        var productSku = await ReadAsync(
            $"api/product-master/skus/{skuId:D}?level={skuLevel}", cancellationToken, wrapped: false);
        if (productSku is null || RequiredGuid(productSku.Value, "skuId") != skuId ||
            RequiredString(productSku.Value, "skuLevel") != skuLevel ||
            RequiredString(productSku.Value, "lifecycleStatus") != "Active" ||
            RequiredString(productSku.Value, "contractVersion") != "v1.1") return invalid;
        var uom = await ReadAsync($"api/product-master/skus/{skuId:D}/uom?level={skuLevel}",
            cancellationToken, wrapped: false);
        if (uom is null || RequiredString(uom.Value, "contractVersion") != "v1.1") return invalid;
        var baseUomId = RequiredString(uom.Value, "baseUomId");
        if (string.IsNullOrWhiteSpace(baseUomId) ||
            RequiredString(productSku.Value, "baseUomId") != baseUomId) return invalid;
        if (originalUomId == baseUomId)
            return new HistoryReferenceResult(HistoryReferenceState.Verified,
                baseUomId, 1m, "PRODUCT-MASTER-BUNDLE/getSkuUom", "v1.1",
                ConversionNumerator: 1m, ConversionDenominator: 1m);
        return ResolveConversion(uom.Value, originalUomId, baseUomId) ?? invalid;
    }

    private static HistoryReferenceResult? ResolveConversion(JsonElement uom, string from, string to)
    {
        if (!uom.TryGetProperty("conversions", out var conversions) ||
            conversions.ValueKind != JsonValueKind.Array) return null;
        var edges = new List<(string From, string To, decimal Numerator, decimal Denominator,
            string Evidence)>();
        foreach (var item in conversions.EnumerateArray())
        {
            if ((item.TryGetProperty("effectiveFrom", out var effectiveFrom) &&
                 effectiveFrom.ValueKind != JsonValueKind.Null) ||
                (item.TryGetProperty("effectiveTo", out var effectiveTo) &&
                 effectiveTo.ValueKind != JsonValueKind.Null)) return null;
            var source = RequiredString(item, "fromUom");
            var target = RequiredString(item, "toUom");
            var numeratorText = RequiredString(item, "numerator");
            var denominatorText = RequiredString(item, "denominator");
            if (source.Length == 0 || target.Length == 0 || source == target ||
                !decimal.TryParse(numeratorText, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var numerator) || numerator <= 0 ||
                !decimal.TryParse(denominatorText, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var denominator) || denominator <= 0)
                return null;
            try
            {
                edges.Add((source, target, numerator, denominator,
                    $"{source}>{target}:{numeratorText}:{denominatorText}"));
                edges.Add((target, source, denominator, numerator,
                    $"{target}>{source}:{denominatorText}:{numeratorText}"));
            }
            catch (OverflowException) { return null; }
        }
        var queue = new Queue<(string Unit, decimal Numerator, decimal Denominator,
            string Evidence, string[] Seen)>();
        queue.Enqueue((from, 1m, 1m, string.Empty, [from]));
        var matches = new List<(decimal Numerator, decimal Denominator, string Evidence)>();
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            if (current.Seen.Length > 4) continue;
            foreach (var edge in edges.Where(edge => edge.From == current.Unit))
            {
                if (current.Seen.Contains(edge.To, StringComparer.Ordinal)) continue;
                decimal numerator;
                decimal denominator;
                try
                {
                    numerator = checked(current.Numerator * edge.Numerator);
                    denominator = checked(current.Denominator * edge.Denominator);
                }
                catch (OverflowException) { return null; }
                var evidence = current.Evidence.Length == 0 ? edge.Evidence :
                    current.Evidence + ">" + edge.Evidence;
                if (edge.To == to) matches.Add((numerator, denominator, evidence));
                else queue.Enqueue((edge.To, numerator, denominator, evidence,
                    [.. current.Seen, edge.To]));
            }
        }
        if (matches.Count == 0) return null;
        try
        {
            if (matches.Any(match => checked(match.Numerator * matches[0].Denominator) !=
                    checked(matches[0].Numerator * match.Denominator))) return null;
        }
        catch (OverflowException) { return null; }
        var chosen = matches.OrderBy(match => match.Evidence, StringComparer.Ordinal).First();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chosen.Evidence)));
        try
        {
            var factor = chosen.Numerator / chosen.Denominator;
            return factor > 0 ? new HistoryReferenceResult(HistoryReferenceState.Verified, to,
                factor, "PRODUCT-MASTER-BUNDLE/getSkuUom/" + hash, "v1.1",
                ConversionNumerator: chosen.Numerator,
                ConversionDenominator: chosen.Denominator) : null;
        }
        catch (OverflowException) { return null; }
    }

    private async Task<bool> IsActiveWarehouseAsync(Guid legalEntityId, string warehouseId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(warehouseId)) return false;
        var location = await ReadAsync($"api/locations/{Uri.EscapeDataString(warehouseId)}",
            cancellationToken, wrapped: false);
        return location is not null &&
               RequiredString(location.Value, "locationId") == warehouseId &&
               RequiredGuid(location.Value, "legalEntityId") == legalEntityId &&
               RequiredString(location.Value, "type") == "Warehouse" &&
               RequiredString(location.Value, "status") == "Active";
    }

    private async Task<JsonElement?> ReadAsync(string path, CancellationToken cancellationToken,
        bool wrapped = true)
    {
        var authorization = _context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caller bearer token is required for reference validation.");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException("Reference source is unavailable.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var data = wrapped ?
            document.RootElement.TryGetProperty("data", out var payload) ? payload : default :
            document.RootElement;
        if (data.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Reference source returned an invalid response.");
        return data.Clone();
    }

    private static string RequiredString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String ?
            field.GetString() ?? throw new InvalidOperationException("Missing reference field.") :
            throw new InvalidOperationException("Missing reference field.");

    private static Guid RequiredGuid(JsonElement value, string name) =>
        Guid.TryParse(RequiredString(value, name), out var result) && result != Guid.Empty ?
            result : throw new InvalidOperationException("Invalid reference identifier.");

    private static bool RequiredBool(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind is JsonValueKind.True or JsonValueKind.False ?
            field.GetBoolean() : throw new InvalidOperationException("Missing reference field.");

    private static bool IsIdentityApproved(JsonElement value)
    {
        if (!value.TryGetProperty("lifecycleStatus", out var state))
            throw new InvalidOperationException("Missing reference field.");
        return state.ValueKind == JsonValueKind.Number ? state.GetInt32() == 3 :
            state.ValueKind == JsonValueKind.String &&
            state.GetString() == "IdentityApproved";
    }
}
