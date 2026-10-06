using System.Net;
using System.Net.Http.Headers;

namespace Diten.Web.Services;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS (step 2) — the ONE place a CRM Web screen reads a MOD-0048 reference set for its dropdowns.
///
/// <para><b>Why.</b> The Platform consumer path (<c>/api/v1/reference-data/sets/{setCode}/published-values</c>) needs
/// <c>Platform.BusinessReferenceData.Consumer.Read</c>, which only an administrator holds, so every other tenant role got
/// empty CRM dropdowns. The Platform consumable-sets route answers the listed sets for any signed-in tenant user, by the
/// set's own scope, without that permission (step 1).</para>
///
/// <para><b>Rule — the same as CRM's <c>GatewayReferenceDataValidator</c>.</b> Ask the consumable-sets route first, with the
/// caller's own <c>Authorization</c> + <c>X-Tenant-Id</c> and NO <c>scope_key</c> (the Platform takes the tenant from the
/// token). A success is the answer; so is 404 <c>reference_set_not_published</c> (the set is listed but has nothing
/// published for this tenant). Only when the Platform says the set is not on its consumable list (404
/// <c>reference_set_not_tenant_accessible</c>) does the read go to the old consumer path, and that set code is remembered
/// for the life of the process (<see cref="CrmReferenceSetRouting"/>). Any other refusal of the consumable route — for
/// example a Platform deployed without it, whose 404 body is <c>{}</c> — sends THAT read to the old path and is not
/// remembered.</para>
///
/// <para><b>The old path</b> is read the way CRM reads it: <c>?scope_key=&lt;tenant&gt;</c> first, and once more without the
/// key only on the consumer service's own <c>scope_key_not_allowed_for_global</c> signal (a GLOBAL set refuses a key, a
/// tenant set requires one, so the retry can never widen a read). Both routes answer
/// <c>Response&lt;BusinessReferenceDataPublishedValuesModel&gt;</c>, so callers keep reading the model they read before.</para>
///
/// <para>The caller owns the returned response. <c>null</c> means no request was made (no tenant) or the Gateway could not be
/// reached — the meaning the controllers' own gateway helpers already give <c>null</c>.</para>
/// </summary>
public sealed class CrmReferenceSetReader
{
    public const string ConsumableSetsPathTemplate =
        "/api/lookups/reference-data/consumable-sets/{setCode}/published-values";

    public const string ConsumerPathTemplate = "/api/v1/reference-data/sets/{setCode}/published-values";

    /// <summary>Consumable-sets route: this set is not on the Platform consumable list — use the consumer path.</summary>
    public const string NotTenantAccessibleSignal = "reference_set_not_tenant_accessible";

    /// <summary>Consumable-sets route: the set is listed but nothing is published for this tenant (final).</summary>
    public const string NotPublishedSignal = "reference_set_not_published";

    /// <summary>Consumer path: the set is global — read it without a scope key.</summary>
    public const string GlobalScopeKeyRefusal = "scope_key_not_allowed_for_global";

    private const string TenantHeaderName = "X-Tenant-Id";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger _logger;
    private readonly CrmReferenceSetRouting _routing;

    /// <param name="httpClient">The controller's own Gateway client.</param>
    /// <param name="gatewayUrl">The configured <c>GatewayUrl</c>.</param>
    /// <param name="logger">The calling controller's logger.</param>
    /// <param name="routing">The not-consumable memory; the process-wide <see cref="CrmReferenceSetRouting.Shared"/> when
    /// omitted (tests pass their own).</param>
    public CrmReferenceSetReader(
        HttpClient httpClient, string gatewayUrl, ILogger logger, CrmReferenceSetRouting? routing = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _gatewayUrl = (gatewayUrl ?? throw new ArgumentNullException(nameof(gatewayUrl))).TrimEnd('/');
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _routing = routing ?? CrmReferenceSetRouting.Shared;
    }

    /// <summary>Reads the published values of <paramref name="setCode"/> for the signed-in tenant user (see class note).</summary>
    public async Task<HttpResponseMessage?> ReadAsync(
        string setCode, string? accessToken, string? tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        try
        {
            if (!_routing.IsKnownNotConsumable(setCode))
            {
                var consumable = await GetAsync(
                    ConsumableSetsPathTemplate.Replace("{setCode}", Uri.EscapeDataString(setCode)),
                    accessToken, tenantId, cancellationToken);

                if (consumable.IsSuccessStatusCode)
                {
                    return consumable;
                }

                var refusal = await ReadBodyAsync(consumable, cancellationToken);
                if (consumable.StatusCode == HttpStatusCode.NotFound
                    && refusal.Contains(NotPublishedSignal, StringComparison.OrdinalIgnoreCase))
                {
                    return consumable;
                }

                consumable.Dispose();
                if (consumable.StatusCode == HttpStatusCode.NotFound
                    && refusal.Contains(NotTenantAccessibleSignal, StringComparison.OrdinalIgnoreCase))
                {
                    _routing.MarkNotConsumable(setCode);
                    _logger.LogDebug(
                        "Reference set '{SetCode}' is not on the Platform consumable list; reading it on the consumer path from now on.",
                        setCode);
                }
                else
                {
                    _logger.LogDebug(
                        "Consumable-sets read of '{SetCode}' returned {Status}; reading it on the consumer path this time.",
                        setCode, consumable.StatusCode);
                }
            }

            return await ReadConsumerPathAsync(setCode, accessToken, tenantId, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' read through the Gateway failed.", setCode);
            return null;
        }
    }

    /// <summary>The old consumer path: tenant scope key first, keyless only on the global-set refusal signal.</summary>
    private async Task<HttpResponseMessage> ReadConsumerPathAsync(
        string setCode, string? accessToken, string tenantId, CancellationToken cancellationToken)
    {
        var basePath = ConsumerPathTemplate.Replace("{setCode}", Uri.EscapeDataString(setCode));

        var scoped = await GetAsync(
            $"{basePath}?scope_key={Uri.EscapeDataString(tenantId)}", accessToken, tenantId, cancellationToken);
        if (scoped.IsSuccessStatusCode
            || !(await ReadBodyAsync(scoped, cancellationToken))
                .Contains(GlobalScopeKeyRefusal, StringComparison.OrdinalIgnoreCase))
        {
            return scoped;
        }

        scoped.Dispose();
        _logger.LogDebug("Reference set '{SetCode}' is global-scoped; retrying the consumer read without scope_key.", setCode);
        return await GetAsync(basePath, accessToken, tenantId, cancellationToken);
    }

    private async Task<HttpResponseMessage> GetAsync(
        string path, string? accessToken, string tenantId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{path}");
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>Reads a refusal body for its signal. The content is buffered, so a response handed back to the caller can
    /// still be read by it.</summary>
    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
