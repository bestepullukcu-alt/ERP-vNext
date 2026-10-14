using System.Net;
using System.Text.Json;
using Diten.InventoryService.Application.Common.Upstream;

namespace Diten.InventoryService.Infrastructure.Upstream;

/// <summary>
/// MOD-0173-FU02 §7: Platform OrganizationUnit lookup (owner MOD-0288) through the Gateway —
/// <c>GET {base}/{id}</c>, caller's token forwarded (permission <c>platform.organization-units.read</c>). Platform
/// wraps its body in <c>{ data: OrganizationUnitDto }</c>.
/// <list type="bullet">
/// <item>200 with <c>legalEntityId</c> = the scope company, <c>status</c> Active, <c>isArchived</c> false → Valid.</item>
/// <item>200 otherwise, 404 (unknown to the caller's tenant), 400 → Invalid (422 ORG_UNIT_INVALID).</item>
/// <item>401 / 403 (the caller may not read org units), 5xx, timeout, network failure or a body outside the shape →
/// <see cref="OrgUnitDirectoryUnavailableException"/> (503, nothing written): the unit could not be CONFIRMED, which
/// is not the same as invalid (ASSUMPTION-LOC1-08).</item>
/// </list>
/// </summary>
public sealed class OrgUnitDirectoryHttpClient : IOrgUnitDirectoryClient
{
    private readonly HttpClient _http;

    public OrgUnitDirectoryHttpClient(HttpClient http) => _http = http;

    public async Task<OrgUnitCheck> CheckAsync(Guid organizationUnitId, Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{organizationUnitId:D}");
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw new OrgUnitDirectoryUnavailableException($"GET {request.RequestUri} failed: {ex.GetType().Name}.", ex);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound or HttpStatusCode.BadRequest:
                    return OrgUnitCheck.Invalid;
                case HttpStatusCode.OK:
                    break;
                default:
                    throw new OrgUnitDirectoryUnavailableException($"GET {request.RequestUri} answered {(int)response.StatusCode}.");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var data = document.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : document.RootElement;
                var id = UpstreamHttp.RequiredString(data, "id");
                var unitLegalEntity = UpstreamHttp.RequiredString(data, "legalEntityId");
                var status = UpstreamHttp.RequiredString(data, "status");
                var archived = UpstreamHttp.RequiredBool(data, "isArchived");

                return Guid.TryParse(id, out var answered) && answered == organizationUnitId
                       && Guid.TryParse(unitLegalEntity, out var unitLe) && unitLe == legalEntityId
                       && string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                       && !archived
                    ? OrgUnitCheck.Valid
                    : OrgUnitCheck.Invalid;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                throw new OrgUnitDirectoryUnavailableException($"GET {request.RequestUri} returned a body outside the organization-unit shape.", ex);
            }
        }
    }
}
