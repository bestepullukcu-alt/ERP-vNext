using System.Net;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

public sealed class LiveHistoryImportReferences : IHistoryImportScopeAuthority, IHistoryRowReferenceChecker
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;

    public LiveHistoryImportReferences(IHttpClientFactory factory, IHttpContextAccessor context)
    {
        _http = factory.CreateClient("DemandGateway");
        _context = context;
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
        return new HistoryReferenceResult(HistoryReferenceState.Verified,
            null, null, null, null, UnitVerificationPending: true);
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
        value.GetProperty(name).GetString() ?? throw new InvalidOperationException("Missing reference field.");

    private static Guid RequiredGuid(JsonElement value, string name) =>
        Guid.TryParse(RequiredString(value, name), out var result) && result != Guid.Empty ?
            result : throw new InvalidOperationException("Invalid reference identifier.");

    private static bool RequiredBool(JsonElement value, string name) =>
        value.GetProperty(name).GetBoolean();

    private static bool IsIdentityApproved(JsonElement value)
    {
        var state = value.GetProperty("lifecycleStatus");
        return state.ValueKind == JsonValueKind.Number ? state.GetInt32() == 3 :
            state.ValueKind == JsonValueKind.String &&
            state.GetString() == "IdentityApproved";
    }
}
