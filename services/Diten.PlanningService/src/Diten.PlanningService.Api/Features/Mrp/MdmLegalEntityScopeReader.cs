using System.Net;
using System.Text.Json;
using Diten.PlanningService.Application.Features.Mrp;
using Diten.PlanningService.Api.Features.DemandPlanning;

namespace Diten.PlanningService.Api.Features.Mrp;

public sealed class MdmLegalEntityScopeReader(IHttpClientFactory clients,
    IHttpContextAccessor contexts) : ILegalEntityScopeReader
{
    public async Task<LegalEntityReferenceResult> ValidateAsync(Guid tenantId, Guid legalEntityId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty)
            return LegalEntityReferenceResult.NotFound;
        try
        {
            using var response = await SendAsync($"api/legal-entities/{legalEntityId:D}/lookup-validation",
                tenantId, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return LegalEntityReferenceResult.NotFound;
            if (response.StatusCode != HttpStatusCode.OK)
                return LegalEntityReferenceResult.Unavailable;
            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var data = json.RootElement.GetProperty("data");
            if (!data.TryGetProperty("legalEntityId", out var id) ||
                id.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(id.GetString(), out var returned) ||
                !data.TryGetProperty("referenceable", out var active) ||
                active.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return LegalEntityReferenceResult.Unavailable;
            return returned == legalEntityId && active.GetBoolean()
                ? LegalEntityReferenceResult.Active : LegalEntityReferenceResult.NotFound;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return LegalEntityReferenceResult.Unavailable; }
    }

    public async Task<IReadOnlyList<Guid>?> ListActiveAsync(Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return null;
        try
        {
            using var response = await SendAsync("api/legal-entities/lookup", tenantId, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK) return null;
            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var data = json.RootElement.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Array) return null;
            var ids = new List<Guid>();
            foreach (var row in data.EnumerateArray())
            {
                if (!row.TryGetProperty("legalEntityId", out var id) ||
                    !Guid.TryParse(id.GetString(), out var parsed) || parsed == Guid.Empty ||
                    !row.TryGetProperty("referenceable", out var active) ||
                    active.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
                if (active.GetBoolean()) ids.Add(parsed);
            }
            return ids.Distinct().ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, Guid tenantId, CancellationToken ct)
    {
        var context = contexts.HttpContext;
        var bearer = context?.Request.Headers.Authorization.ToString();
        if (context?.Items["DemandPlanning.TenantId"] is not Guid current || current != tenantId ||
            string.IsNullOrWhiteSpace(bearer) ||
            !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caller tenant and bearer token are required.");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        PlanningCorrelation.Forward(context, request);
        request.Headers.TryAddWithoutValidation("Authorization", bearer);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString("D"));
        return await clients.CreateClient("DemandGateway").SendAsync(request, ct);
    }
}
