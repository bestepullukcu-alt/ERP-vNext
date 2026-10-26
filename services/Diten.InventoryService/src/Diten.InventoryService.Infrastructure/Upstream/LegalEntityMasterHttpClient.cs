using System.Net;
using System.Text.Json;
using Diten.InventoryService.Application.Common.Upstream;

namespace Diten.InventoryService.Infrastructure.Upstream;

/// <summary>
/// Typed client for MDM <c>GET /api/legal-entities/{id}/lookup-validation</c> through the Gateway (W-0173-02), with the
/// caller's own bearer forwarded (<see cref="ForwardCallerHeadersHandler"/>) so MDM scopes the lookup to the caller's
/// tenant. MDM answers <c>Response&lt;LegalEntityLookupDto&gt;</c>: 200 for an ACTIVE entity of that tenant, 404 for
/// anything else (unknown, inactive, another tenant's).
/// <list type="bullet">
/// <item>200 with the same id and <c>referenceable</c> → <see cref="LegalEntityMembership.Active"/>.</item>
/// <item>404, or a 200 that names another id / is not referenceable → <see cref="LegalEntityMembership.NotReferenceable"/>.</item>
/// <item>401 / 403 → <see cref="LegalEntityMembership.CallerRefused"/>.</item>
/// <item>Anything else — refused connection, timeout, 5xx, other status, unparseable body → unavailable (fail-closed).</item>
/// </list>
/// </summary>
public sealed class LegalEntityMasterHttpClient : ILegalEntityMasterClient
{
    private readonly HttpClient _http;

    public LegalEntityMasterHttpClient(HttpClient http) => _http = http;

    public async Task<LegalEntityMembership> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{legalEntityId:D}/lookup-validation");
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
            throw new LegalEntityMasterUnavailableException($"GET {request.RequestUri} failed: {ex.GetType().Name}.", ex);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return LegalEntityMembership.NotReferenceable;
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    return LegalEntityMembership.CallerRefused;
                case HttpStatusCode.OK:
                    break;
                default:
                    throw new LegalEntityMasterUnavailableException($"GET {request.RequestUri} answered {(int)response.StatusCode}.");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var data = document.RootElement.TryGetProperty("data", out var d) ? d : document.RootElement;
                var id = UpstreamHttp.RequiredString(data, "legalEntityId");
                var referenceable = UpstreamHttp.OptionalBool(data, "referenceable");
                return Guid.TryParse(id, out var answered) && answered == legalEntityId && referenceable
                    ? LegalEntityMembership.Active
                    : LegalEntityMembership.NotReferenceable;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                throw new LegalEntityMasterUnavailableException($"GET {request.RequestUri} returned a body outside the lookup shape.", ex);
            }
        }
    }

    /// <summary>
    /// WP-MVP1-FIX-LIVE2: MDM <c>GET /api/legal-entities/{id}</c> (<c>LegalEntityDetailDto</c>). Only a 200 that names the same id
    /// with a code and a name is a name; every other answer (404, 401 / 403, 5xx, timeout, refused connection, another id, a
    /// missing code / name, an unparseable body) is null — "okunamadı", never an invented name. <see cref="ValidateAsync"/> is
    /// untouched.
    /// </summary>
    public async Task<LegalEntityName?> GetLegalEntityNameAsync(Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{legalEntityId:D}");
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var data = document.RootElement.TryGetProperty("data", out var d) ? d : document.RootElement;
            if (data.ValueKind != JsonValueKind.Object
                || !Guid.TryParse(UpstreamHttp.OptionalString(data, "legalEntityId"), out var answered) || answered != legalEntityId)
            {
                return null;
            }

            var code = UpstreamHttp.OptionalString(data, "code");
            var name = UpstreamHttp.OptionalString(data, "displayName") is { Length: > 0 } display
                ? display
                : UpstreamHttp.OptionalString(data, "legalName");
            return string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) ? null : new LegalEntityName(legalEntityId, code, name);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
