using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.ReferenceData;

public sealed class PlatformVerifiedGskuResolverClient : IVerifiedGskuReferenceResolver
{
    private const string RelativePath = "api/internal/v1/reference-data/verified-gsku/resolve";
    private const string EnumerateUomRelativePath = "api/internal/v1/reference-data/verified-gsku/enumerate-uom";
    private const string CredentialIdHeader = "X-Verified-Gsku-Credential-Id";
    private const string CredentialSecretHeader = "X-Verified-Gsku-Credential";
    private const string AudienceHeader = "X-Verified-Gsku-Audience";
    private const string Audience = "VERIFIED_GSKU_RESOLVE";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly VerifiedGskuResolverOptions _options;

    public PlatformVerifiedGskuResolverClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        IOptions<VerifiedGskuResolverOptions> options)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public async Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
        string packApplicabilityValueCode,
        string uomValueCode,
        CancellationToken cancellationToken = default)
    {
        if (_options.PlatformBaseAddress is null
            || !_options.PlatformBaseAddress.IsAbsoluteUri
            || _options.Timeout <= TimeSpan.Zero
            || _options.Timeout > TimeSpan.FromSeconds(2)
            || string.IsNullOrWhiteSpace(_options.CredentialIdentifier)
            || string.IsNullOrEmpty(_options.CredentialSecret))
        {
            return VerifiedGskuReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_CONFIGURATION_INVALID");
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var inboundAuthorization = httpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (httpContext?.User.Identity?.IsAuthenticated != true
            || !AuthenticationHeaderValue.TryParse(inboundAuthorization, out var delegated)
            || !string.Equals(delegated.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(delegated.Parameter))
        {
            return VerifiedGskuReferenceResolveResult.Fail(401, "REFERENCE_UNAUTHENTICATED");
        }

        var endpoint = new Uri(_options.PlatformBaseAddress, RelativePath);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = delegated;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        request.Content = JsonContent.Create(new ResolveRequest(
        [
            new("pack-applicability", packApplicabilityValueCode, "LATEST"),
            new("uom", uomValueCode, "LATEST")
        ]));

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                budget.Token);
            ResolveEnvelope? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<ResolveEnvelope>(cancellationToken: budget.Token);
            }
            catch (System.Text.Json.JsonException)
            {
                return (int)response.StatusCode >= 500
                    ? VerifiedGskuReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
                    : VerifiedGskuReferenceResolveResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH");
            }

            if (response.IsSuccessStatusCode
                && envelope?.IsSuccessful == true
                && envelope.Data?.Selections is { Count: > 0 } selections
                && IsTrustedEvidence(selections, packApplicabilityValueCode, uomValueCode))
            {
                return VerifiedGskuReferenceResolveResult.Success(selections.Select(Map).ToList());
            }

            if (response.IsSuccessStatusCode)
            {
                return VerifiedGskuReferenceResolveResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH");
            }

            return MapFailure(response.StatusCode, envelope?.ReasonCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return VerifiedGskuReferenceResolveResult.Fail(504, "REFERENCE_PROVIDER_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return VerifiedGskuReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
    }

    public async Task<VerifiedGskuUomEnumerationResult> EnumerateUomsAsync(
        CancellationToken cancellationToken = default)
    {
        var preflight = TryCreateRequest(HttpMethod.Post, EnumerateUomRelativePath);
        if (preflight.FailureCode is not null)
        {
            return VerifiedGskuUomEnumerationResult.Fail(preflight.StatusCode, preflight.FailureCode);
        }

        using var request = preflight.Request!;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                budget.Token);
            UomEnvelope? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<UomEnvelope>(cancellationToken: budget.Token);
            }
            catch (System.Text.Json.JsonException)
            {
                return (int)response.StatusCode >= 500
                    ? VerifiedGskuUomEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
                    : VerifiedGskuUomEnumerationResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH");
            }

            if (response.IsSuccessStatusCode
                && envelope?.IsSuccessful == true
                && envelope.Data?.Uoms is { } uoms
                && IsTrustedUomEnumeration(uoms))
            {
                return VerifiedGskuUomEnumerationResult.Success(
                    uoms.Select(x => new VerifiedGskuUom(
                        x.Code,
                        x.DisplayText,
                        x.SortOrder,
                        x.MaximumDecimalPrecision)).ToList());
            }

            if (response.IsSuccessStatusCode)
            {
                return VerifiedGskuUomEnumerationResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH");
            }

            var failure = MapFailure(response.StatusCode, envelope?.ReasonCode);
            return VerifiedGskuUomEnumerationResult.Fail(failure.StatusCode, failure.FailureCode!);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return VerifiedGskuUomEnumerationResult.Fail(504, "REFERENCE_PROVIDER_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return VerifiedGskuUomEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
    }

    private RequestPreflight TryCreateRequest(HttpMethod method, string relativePath)
    {
        if (_options.PlatformBaseAddress is null
            || !_options.PlatformBaseAddress.IsAbsoluteUri
            || _options.Timeout <= TimeSpan.Zero
            || _options.Timeout > TimeSpan.FromSeconds(2)
            || string.IsNullOrWhiteSpace(_options.CredentialIdentifier)
            || string.IsNullOrEmpty(_options.CredentialSecret))
        {
            return RequestPreflight.Fail(503, "REFERENCE_PROVIDER_CONFIGURATION_INVALID");
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var inboundAuthorization = httpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (httpContext?.User.Identity?.IsAuthenticated != true
            || !AuthenticationHeaderValue.TryParse(inboundAuthorization, out var delegated)
            || !string.Equals(delegated.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(delegated.Parameter))
        {
            return RequestPreflight.Fail(401, "REFERENCE_UNAUTHENTICATED");
        }

        var request = new HttpRequestMessage(method, new Uri(_options.PlatformBaseAddress, relativePath));
        request.Headers.Authorization = delegated;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        return RequestPreflight.Success(request);
    }

    private static bool IsTrustedUomEnumeration(IReadOnlyList<UomItem> uoms)
    {
        var expected = new (string Code, int SortOrder, int Precision)[]
        {
            ("C62", 10, 0),
            ("GRM", 20, 3),
            ("KGM", 30, 3),
            ("MLT", 40, 3),
            ("LTR", 50, 3)
        };
        return uoms.Count == expected.Length
               && uoms.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == expected.Length
               && uoms.OrderBy(x => x.SortOrder).Select((item, index) =>
                   string.Equals(item.Code, expected[index].Code, StringComparison.Ordinal)
                   && item.SortOrder == expected[index].SortOrder
                   && item.MaximumDecimalPrecision == expected[index].Precision
                   && !string.IsNullOrWhiteSpace(item.DisplayText)).All(x => x);
    }

    private static bool IsTrustedEvidence(
        IReadOnlyList<ResolveSelection> selections,
        string requestedPackApplicability,
        string requestedUom)
    {
        if (selections.Count != 2
            || selections.Select(x => x.SetCode).Distinct(StringComparer.Ordinal).Count() != 2)
        {
            return false;
        }

        return selections.All(selection =>
            selection.CatalogVersionId != Guid.Empty
            && selection.CatalogVersionNumber > 0
            && string.Equals(selection.ResolutionMode, "LATEST", StringComparison.Ordinal)
            && selection.ResolvedAtUtc != default
            && !selection.IsRetired
            && selection.SelectableForNew)
            && selections.Any(x =>
                string.Equals(x.SetCode, "pack-applicability", StringComparison.Ordinal)
                && string.Equals(x.ValueCode, requestedPackApplicability, StringComparison.Ordinal)
                && string.Equals(x.ValueCode, "SCALAR_QUANTITY_APPLIES", StringComparison.Ordinal))
            && selections.Any(x =>
                string.Equals(x.SetCode, "uom", StringComparison.Ordinal)
                && string.Equals(x.ValueCode, requestedUom, StringComparison.Ordinal)
                && x.ValueCode is "C62" or "GRM" or "KGM" or "MLT" or "LTR");
    }

    private static VerifiedGskuReferenceSelection Map(ResolveSelection selection) => new(
        selection.SetCode,
        selection.ValueCode,
        selection.CatalogVersionId,
        selection.CatalogVersionNumber,
        selection.ResolutionMode,
        selection.ResolvedAtUtc,
        selection.IsRetired,
        selection.SelectableForNew);

    private static VerifiedGskuReferenceResolveResult MapFailure(HttpStatusCode status, string? reasonCode)
    {
        var statusCode = (int)status;
        var fallback = statusCode switch
        {
            401 => "REFERENCE_UNAUTHENTICATED",
            403 => "REFERENCE_FORBIDDEN",
            404 => "REFERENCE_SET_NOT_ACCESSIBLE",
            409 => "REFERENCE_RESOLUTION_CONTRACT_INVALID",
            504 => "REFERENCE_PROVIDER_TIMEOUT",
            _ => "REFERENCE_PROVIDER_UNAVAILABLE"
        };

        var trustedReason = !string.IsNullOrWhiteSpace(reasonCode)
            && reasonCode.StartsWith("REFERENCE_", StringComparison.Ordinal)
            ? reasonCode
            : fallback;
        return VerifiedGskuReferenceResolveResult.Fail(
            statusCode is 401 or 403 or 404 or 409 or 503 or 504 ? statusCode : 503,
            trustedReason);
    }

    private sealed record ResolveRequest(
        [property: JsonPropertyName("selections")] IReadOnlyList<ResolveSelectionRequest> Selections);

    private sealed record ResolveSelectionRequest(
        [property: JsonPropertyName("set_code")] string SetCode,
        [property: JsonPropertyName("value_code")] string ValueCode,
        [property: JsonPropertyName("resolution_mode")] string ResolutionMode);

    private sealed class ResolveEnvelope
    {
        public bool IsSuccessful { get; init; }
        public ResolveData? Data { get; init; }

        [JsonPropertyName("reason_code")]
        public string? ReasonCode { get; init; }
    }

    private sealed record ResolveData(
        [property: JsonPropertyName("selections")] IReadOnlyList<ResolveSelection> Selections);

    private sealed record ResolveSelection(
        [property: JsonPropertyName("set_code")] string SetCode,
        [property: JsonPropertyName("value_code")] string ValueCode,
        [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
        [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
        [property: JsonPropertyName("resolution_mode")] string ResolutionMode,
        [property: JsonPropertyName("resolved_at_utc")] DateTimeOffset ResolvedAtUtc,
        [property: JsonPropertyName("is_retired")] bool IsRetired,
        [property: JsonPropertyName("selectable_for_new")] bool SelectableForNew);

    private sealed class UomEnvelope
    {
        public bool IsSuccessful { get; init; }
        public UomData? Data { get; init; }

        [JsonPropertyName("reason_code")]
        public string? ReasonCode { get; init; }
    }

    private sealed record UomData(
        [property: JsonPropertyName("uoms")] IReadOnlyList<UomItem> Uoms);

    private sealed record UomItem(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("display_text")] string DisplayText,
        [property: JsonPropertyName("sort_order")] int SortOrder,
        [property: JsonPropertyName("maximum_decimal_precision")] int MaximumDecimalPrecision);

    private sealed record RequestPreflight(
        HttpRequestMessage? Request,
        int StatusCode,
        string? FailureCode)
    {
        public static RequestPreflight Success(HttpRequestMessage request) => new(request, 200, null);
        public static RequestPreflight Fail(int statusCode, string failureCode) => new(null, statusCode, failureCode);
    }
}
