using System.Net.Http.Json;
using Diten.InventoryService.Application.Common.Upstream;

namespace Diten.InventoryService.Infrastructure.Upstream;

/// <summary>
/// Typed HTTP client for LOCATION v1 (frozen, shared Location master — DEC-INV-03). Paths are the contract's,
/// relative to the configured base URL: <c>POST validate</c>, <c>GET {id}</c>, <c>GET {id}/path</c>.
/// </summary>
public sealed class LocationMasterHttpClient : ILocationMasterClient
{
    private readonly HttpClient _http;

    public LocationMasterHttpClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<LocationValidation>> ValidateLocationsAsync(IReadOnlyList<string> locationIds, CancellationToken cancellationToken = default)
    {
        var results = await UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Post, "validate") { Content = JsonContent.Create(new { locationIds }) },
            notFoundIsAbsent: false,
            root => UpstreamHttp.RequiredArray(root, "results").EnumerateArray()
                .Select(r => new LocationValidation(
                    UpstreamHttp.RequiredString(r, "locationId"),
                    UpstreamHttp.OptionalBool(r, "exists"),
                    UpstreamHttp.NullableBool(r, "isStockable")))
                .ToList(),
            Unavailable,
            cancellationToken);
        return results!;
    }

    public Task<LocationReference?> GetLocationAsync(string locationId, CancellationToken cancellationToken = default)
        => UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Get, Uri.EscapeDataString(locationId)),
            notFoundIsAbsent: true,
            root => new LocationReference(
                UpstreamHttp.RequiredString(root, "locationId"),
                UpstreamHttp.OptionalString(root, "type"),
                UpstreamHttp.OptionalString(root, "legalEntityId"),
                UpstreamHttp.OptionalString(root, "status"),
                UpstreamHttp.OptionalBool(root, "isStockable")),
            Unavailable,
            cancellationToken);

    public Task<LocationPath?> GetLocationPathAsync(string locationId, CancellationToken cancellationToken = default)
        => UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Get, $"{Uri.EscapeDataString(locationId)}/path"),
            notFoundIsAbsent: true,
            root => new LocationPath(
                UpstreamHttp.OptionalString(root, "locationId"),
                UpstreamHttp.RequiredArray(root, "path").EnumerateArray()
                    .Select(p => new LocationPathEntry(
                        UpstreamHttp.OptionalString(p, "type"),
                        UpstreamHttp.OptionalString(p, "id"),
                        UpstreamHttp.OptionalString(p, "code")))
                    .ToList()),
            Unavailable,
            cancellationToken);

    private static Exception Unavailable(string message, Exception? inner) => new LocationMasterUnavailableException(message, inner);
}
