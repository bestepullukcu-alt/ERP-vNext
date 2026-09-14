using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.HumanCapitalService.Infrastructure.LegalEntities;

/// <summary>
/// Typed client that fetches the tenant's legal-entity list (id + parentId) from MDM, propagating the
/// caller's bearer token so MDM authorizes the read under the same identity. Fail-safe: any failure
/// yields an empty snapshot so the hierarchy cache degrades to self-only scoping rather than throwing.
/// </summary>
public sealed class MdmLegalEntityHierarchyClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MdmLegalEntityHierarchyClient(
        HttpClient httpClient,
        IOptions<MdmServiceOptions> options,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            throw new InvalidOperationException("Configuration error: 'MdmService:BaseUrl' is missing.");
        }

        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
    }

    /// <summary>Returns the (id, parentId) edges for every legal entity visible to the caller.</summary>
    public async Task<IReadOnlyList<LegalEntityEdge>> GetEdgesAsync(CancellationToken ct)
    {
        PropagateAuthorizationHeader();

        try
        {
            using var response = await _httpClient.GetAsync("api/legal-entities", ct);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<LegalEntityEdge>();
            }

            var envelope = await response.Content.ReadFromJsonAsync<MdmResponse<List<LegalEntityListItem>>>(cancellationToken: ct);
            if (envelope?.IsSuccessful != true || envelope.Data is null)
            {
                return Array.Empty<LegalEntityEdge>();
            }

            return envelope.Data
                .Where(item => item.LegalEntityId != Guid.Empty)
                .Select(item => new LegalEntityEdge(item.LegalEntityId, item.ParentId))
                .ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return Array.Empty<LegalEntityEdge>();
        }
    }

    private void PropagateAuthorizationHeader()
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            _httpClient.DefaultRequestHeaders.Authorization = parsed;
        }
    }

    public readonly record struct LegalEntityEdge(Guid Id, Guid? ParentId);

    private sealed record LegalEntityListItem(Guid LegalEntityId, Guid? ParentId);

    private sealed record MdmResponse<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string>? Errors);
}
