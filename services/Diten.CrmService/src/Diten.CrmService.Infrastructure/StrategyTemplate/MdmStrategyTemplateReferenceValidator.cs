using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 cross-service validator: proves that an MDM global product or Gsku bound by a product line EXISTS
/// before the binding is allowed to be authored. It derives nothing — the only question it asks is "does this id
/// exist?" — and it never writes to MDM.
/// <para><b>Deliberately has no cache.</b> The same id asked twice makes two calls. A cache here would mean a play could
/// be authored against a reference that no longer exists, which is precisely the thing this class is for.</para>
/// <para>The transport profile mirrors the MOD-0167 FU02 segment validator verbatim: 3 second total timeout, ONE
/// transient retry (502/503/504, 75 ms), Authorization / X-Tenant-Id / X-Correlation-Id forwarded, and always through
/// the Gateway — never a service port. The proof therefore runs with the CALLER's token: an author lacking
/// <c>mdm.global-products.read</c> / <c>mdm.gskus.read</c> gets a 503, not a silent pass (F-MDM-PERM).</para>
/// <para><b>404 and unreachable are different answers.</b> 404 means the dependency spoke and the binding is not
/// authorable (400). A timeout, a 5xx, an auth rejection or a malformed body means we do not know, which is a 503 with
/// nothing persisted — the caller invokes this BEFORE any insert or replace, so a dependency outage can never leave a
/// half-authored play behind.</para>
/// <para>There is no brand path here (D-BRAND) and no attempt to check that a Gsku belongs to a global product
/// (D-SKU-LINK): MDM's Gsku carries no GlobalProductId and this FU may not open a new MDM read surface.</para>
/// </summary>
public sealed class MdmStrategyTemplateReferenceValidator : IStrategyTemplateProductReferenceValidator
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly string _globalProductPath;
    private readonly string _gskuPath;

    public MdmStrategyTemplateReferenceValidator(
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
        // already publishes (/api/global-products/{everything} and /api/gskus/{everything}); no new route is needed.
        _globalProductPath = configuration["StrategyTemplate:GlobalProductPathTemplate"]
            ?? "api/global-products/{id}";
        _gskuPath = configuration["StrategyTemplate:GskuPathTemplate"] ?? "api/gskus/{id}";
    }

    public async Task<IStrategyTemplateProductReferenceValidator.Outcome> ValidateAsync(
        string referenceKind, Guid referenceId, CancellationToken cancellationToken)
    {
        if (referenceId == Guid.Empty)
        {
            return IStrategyTemplateProductReferenceValidator.Outcome.NotFound;
        }

        var template = referenceKind switch
        {
            IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct => _globalProductPath,
            IStrategyTemplateProductReferenceValidator.ReferenceKind.Gsku => _gskuPath,
            _ => null
        };

        if (template is null)
        {
            // An unknown reference kind is a coding error, not a dependency outage; refuse rather than pass.
            return IStrategyTemplateProductReferenceValidator.Outcome.NotFound;
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
                    return IStrategyTemplateProductReferenceValidator.Outcome.NotFound;
                }

                // A tenant mismatch, an auth rejection and an exhausted transient failure are all the same thing for
                // this write: the caller cannot PROVE the reference, so persistence is forbidden.
                if (!response.IsSuccessStatusCode)
                {
                    return IStrategyTemplateProductReferenceValidator.Outcome.Unavailable;
                }

                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                return HasReferencedEntity(body)
                    ? IStrategyTemplateProductReferenceValidator.Outcome.Valid
                    : IStrategyTemplateProductReferenceValidator.Outcome.NotFound;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or NotSupportedException)
        {
            return IStrategyTemplateProductReferenceValidator.Outcome.Unavailable;
        }

        return IStrategyTemplateProductReferenceValidator.Outcome.Unavailable;
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
