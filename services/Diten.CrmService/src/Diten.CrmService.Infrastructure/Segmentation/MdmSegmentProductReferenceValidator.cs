using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Segmentation;

/// <summary>
/// MOD-0167 FU02 class-X validator: proves that a criterion VALUE (an MDM global product, product or brand) exists
/// before the criterion is allowed to be authored. It never derives membership — for <c>concept.affinity</c> membership
/// comes from the in-service concept graph, and MDM is only asked "does this id exist?".
/// <para><b>Deliberately has no cache.</b> The same id asked twice makes two calls. A cache here would mean a criterion
/// could be authored against a reference that no longer exists, which is precisely the thing this class is for.</para>
/// <para>The transport profile mirrors the Working Calendar legal-entity validator verbatim: 3 second total timeout,
/// ONE transient retry (502/503/504, 75 ms), Authorization / X-Tenant-Id / X-Correlation-Id forwarded, and always
/// through the Gateway — never a service port.</para>
/// <para><b>404 and unreachable are different answers.</b> 404 means the dependency spoke and the rule is not
/// authorable (400). A timeout, a 5xx, an auth rejection or a malformed body means we do not know, which is a 503 with
/// nothing persisted — the caller invokes this BEFORE any insert or replace, so a dependency outage can never leave a
/// half-authored segment behind.</para>
/// </summary>
public sealed class MdmSegmentProductReferenceValidator : ISegmentProductReferenceValidator
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly string _globalProductPath;
    private readonly string _productPath;
    private readonly string _brandPath;

    public MdmSegmentProductReferenceValidator(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;

        var gatewayBaseUrl = configuration["Gateway:BaseUrl"]
            ?? configuration["GatewayUrl"]
            ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl.TrimEnd('/') + "/");

        // Configurable so a route move is an ops change, not a code change. The defaults are the routes the gateway
        // already publishes, and they are the same surfaces the MOD-0162 FU03 external-reference picker uses.
        _globalProductPath = configuration["Segmentation:GlobalProductPathTemplate"]
            ?? "api/global-products/{id}";
        _productPath = configuration["Segmentation:ProductPathTemplate"] ?? "api/mdm/products/{id}";
        _brandPath = configuration["Segmentation:BrandPathTemplate"] ?? "api/mdm/brands/{id}";
    }

    public async Task<ISegmentProductReferenceValidator.Outcome> ValidateAsync(
        string referenceKind, Guid referenceId, CancellationToken cancellationToken)
    {
        if (referenceId == Guid.Empty)
        {
            return ISegmentProductReferenceValidator.Outcome.NotFound;
        }

        var template = referenceKind switch
        {
            SegmentAttributeCatalog.ReferenceKindGlobalProduct => _globalProductPath,
            SegmentAttributeCatalog.ReferenceKindProduct => _productPath,
            SegmentAttributeCatalog.ReferenceKindBrand => _brandPath,
            _ => null
        };

        if (template is null)
        {
            // An unknown reference kind is a coding error, not a dependency outage; refuse rather than pass.
            return ISegmentProductReferenceValidator.Outcome.NotFound;
        }

        var path = template.Replace("{id}", referenceId.ToString("D"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TotalTimeout);

        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                ForwardContextHeaders(request);

                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (IsTransient(response.StatusCode) && attempt == 0)
                {
                    await Task.Delay(RetryDelay, timeout.Token);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return ISegmentProductReferenceValidator.Outcome.NotFound;
                }

                // A tenant mismatch, an auth rejection and an exhausted transient failure are all the same thing for
                // this write: the caller cannot PROVE the reference, so persistence is forbidden.
                if (!response.IsSuccessStatusCode)
                {
                    return ISegmentProductReferenceValidator.Outcome.Unavailable;
                }

                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                return HasReferencedEntity(body)
                    ? ISegmentProductReferenceValidator.Outcome.Valid
                    : ISegmentProductReferenceValidator.Outcome.NotFound;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or NotSupportedException)
        {
            return ISegmentProductReferenceValidator.Outcome.Unavailable;
        }

        return ISegmentProductReferenceValidator.Outcome.Unavailable;
    }

    /// <summary>A 200 with an empty or unsuccessful envelope is NOT a proof of existence.</summary>
    private static bool HasReferencedEntity(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("isSuccessful", out var successful)
            && successful.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (!root.TryGetProperty("data", out var data))
        {
            // A bare entity body (no envelope) still counts, as long as it carries something.
            return root.EnumerateObject().Any();
        }

        return data.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
               && (data.ValueKind != JsonValueKind.Object || data.EnumerateObject().Any());
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;

        var authorization = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
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

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}
