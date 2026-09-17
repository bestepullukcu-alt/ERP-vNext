using System.Net.Http.Json;
using Diten.CrmService.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.CrmService.Infrastructure.Auth;

/// <summary>
/// S2S display-name resolver (WP-SEG-DETAILS6). Calls AuthService <c>internal/users/display-names</c> with the shared
/// <c>X-Internal-Api-Key</c>, exactly as Platform's own <c>AuthUserDisplayNameClient</c> does. It reads DISPLAY NAMES
/// ONLY — the endpoint returns no email and this client never asks the sibling <c>contacts</c> route.
/// <para><b>One bulk call.</b> The whole id set travels in a single request (chunked only past
/// <see cref="ChunkSize"/>, far above the three provenance ids a segment carries), so a detail read is one round trip
/// regardless of how many actors it names.</para>
/// <para><b>Tenant safety.</b> The tenant id comes from the server-side <see cref="ITenantContext"/>, never a caller
/// value, and AuthService scopes the sweep by it again — a foreign id is simply never in the answer.</para>
/// <para><b>Fail-closed.</b> A missing configuration, an unreachable AuthService, a non-2xx status or a malformed body
/// all resolve to "no names": the timeline then shows a date without a name, never a raw id and never a guess.</para>
/// </summary>
public sealed class AuthUserDisplayNameClient : IUserDisplayNameResolver
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    /// <summary>Ids per request. Keeps the query string bounded while staying far from one-call-per-user.</summary>
    private const int ChunkSize = 100;

    private readonly HttpClient _httpClient;
    private readonly AuthServiceOptions _options;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AuthUserDisplayNameClient> _logger;

    public AuthUserDisplayNameClient(
        HttpClient httpClient,
        IOptions<AuthServiceOptions> options,
        ITenantContext tenantContext,
        ILogger<AuthUserDisplayNameClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var resolved = new Dictionary<Guid, string>();

        var wanted = (userIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return resolved;
        }

        if (_tenantContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            // No tenant context, no scoped lookup: names stay absent rather than risking a cross-tenant read.
            return resolved;
        }

        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning(
                "Cannot resolve user display names; AuthService BaseUrl/InternalApiKey not configured. Names will be omitted.");
            return resolved;
        }

        for (var offset = 0; offset < wanted.Count; offset += ChunkSize)
        {
            var chunk = wanted.Skip(offset).Take(ChunkSize).ToList();
            foreach (var entry in await FetchChunkAsync(tenantId, chunk, cancellationToken))
            {
                resolved[entry.Key] = entry.Value;
            }
        }

        return resolved;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> FetchChunkAsync(
        Guid tenantId, IReadOnlyCollection<Guid> chunk, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/internal/users/display-names"
                      + $"?tenantId={tenantId}&ids={string.Join(',', chunk)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(InternalApiKeyHeader, _options.InternalApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthService display-name request failed. StatusCode={StatusCode} Count={Count}",
                    (int)response.StatusCode, chunk.Count);
                return new Dictionary<Guid, string>();
            }

            var payload = await response.Content
                .ReadFromJsonAsync<List<AuthUserDisplayName>>(cancellationToken: cancellationToken);

            return payload is null
                ? new Dictionary<Guid, string>()
                : payload
                    .Where(entry => entry.Id != Guid.Empty && !string.IsNullOrWhiteSpace(entry.DisplayName))
                    .GroupBy(entry => entry.Id)
                    .ToDictionary(group => group.Key, group => group.First().DisplayName);
        }
        catch (Exception ex)
        {
            // Fail-closed by contract: the segment detail must still render, with the actor name simply absent, when
            // AuthService is down or answers something we cannot read.
            _logger.LogWarning(ex, "AuthService display-name request threw; names will be omitted for this batch.");
            return new Dictionary<Guid, string>();
        }
    }

    private sealed record AuthUserDisplayName(Guid Id, string DisplayName);
}
