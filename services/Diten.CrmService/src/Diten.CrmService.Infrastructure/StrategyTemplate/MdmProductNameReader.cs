using System.Net.Http.Headers;
using System.Net.Http.Json;
using Diten.CrmService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.StrategyTemplate;

/// <summary>
/// WP-VP-4G (F4-4) — <see cref="IProductNameReader"/> over the MDM Global Product SELECTOR the play editor already reads
/// (<c>GET api/global-products/selector</c>: id, canonical code, name), through the Gateway with the caller's
/// Authorization / X-Tenant-Id / X-Correlation-Id — the same door and transport as the MDM reference validator, no new
/// dependency.
/// <para><b>One logical read for any number of ids.</b> The selector has no id filter, so the reader walks its pages
/// (100 per page, the MDM ceiling) until every asked id is found or the catalogue ends — the request count depends on the
/// tenant's catalogue size (a 176-product tenant: 2 pages), never on how many products a plan names. At most
/// <see cref="MaxPages"/> pages; a 3-second total budget.</para>
/// <para><b>Fail-open:</b> any refusal / timeout / unreadable answer stops the walk and returns the names found so far
/// (possibly none). It never throws into a read.</para>
/// </summary>
public sealed class MdmProductNameReader : IProductNameReader
{
    public const int PageSize = 100;
    public const int MaxPages = 20;
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly string _selectorPath;

    public MdmProductNameReader(
        HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ITenantContext tenantContext)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        var gatewayBaseUrl = configuration["Gateway:BaseUrl"] ?? configuration["GatewayUrl"] ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
        _selectorPath = configuration["StrategyTemplate:GlobalProductSelectorPath"] ?? "api/global-products/selector";
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ReadNamesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        var wanted = productIds.Where(id => id != Guid.Empty).ToHashSet();
        if (wanted.Count == 0 || _tenantContext.TenantId is null)
        {
            return names;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TotalTimeout);
        try
        {
            for (var page = 1; page <= MaxPages && names.Count < wanted.Count; page++)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get, $"{_selectorPath}?pageNumber={page}&pageSize={PageSize}");
                ForwardContextHeaders(request);
                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    break;
                }

                var envelope = await response.Content.ReadFromJsonAsync<Envelope>(cancellationToken: timeout.Token);
                var items = envelope?.Data?.Items;
                if (items is null || items.Count == 0)
                {
                    break;
                }

                foreach (var item in items.Where(i => wanted.Contains(i.Id) && !string.IsNullOrWhiteSpace(i.GlobalProductName)))
                {
                    names[item.Id] = item.GlobalProductName!.Trim();
                }

                if (items.Count < PageSize || (envelope!.Data!.TotalCount is { } total && page * PageSize >= total))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Fail-open: a name is display only — the codes are shown.
        }

        return names;
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        var authorization = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }

        var tenant = context?.Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenant) && _tenantContext.TenantId is { } tenantId)
        {
            tenant = tenantId.ToString();
        }

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        }

        var correlation = context?.Request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrWhiteSpace(correlation))
        {
            correlation = context?.TraceIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(correlation))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
        }
    }

    private sealed record Envelope(Page? Data);

    private sealed record Page(IReadOnlyList<Item>? Items, long? TotalCount);

    private sealed record Item(Guid Id, string? CanonicalCode, string? GlobalProductName);
}
