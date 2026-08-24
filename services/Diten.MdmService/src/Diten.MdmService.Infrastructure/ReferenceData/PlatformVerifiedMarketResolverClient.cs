using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.ReferenceData;

public sealed class PlatformVerifiedMarketResolverClient : IVerifiedMarketReferenceResolver
{
    private const string RelativePath = "api/internal/v1/reference-data/verified-market/resolve";
    private const string EnumerateActiveRelativePath =
        "api/internal/v1/reference-data/verified-market/enumerate-active";
    private const string CredentialIdHeader = "X-Verified-Gsku-Credential-Id";
    private const string CredentialSecretHeader = "X-Verified-Gsku-Credential";
    private const string AudienceHeader = "X-Verified-Gsku-Audience";
    private const string Audience = "VERIFIED_GSKU_RESOLVE";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly VerifiedMarketResolverOptions _options;

    public PlatformVerifiedMarketResolverClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        IOptions<VerifiedMarketResolverOptions> options)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public async Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(
        string marketCode,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return Fail(503, "REFERENCE_PROVIDER_CONFIGURATION_INVALID");
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var inboundAuthorization = httpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (httpContext?.User.Identity?.IsAuthenticated != true
            || !AuthenticationHeaderValue.TryParse(inboundAuthorization, out var delegated)
            || !string.Equals(delegated.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(delegated.Parameter))
        {
            return Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.PlatformBaseAddress!, RelativePath));
        request.Headers.Authorization = delegated;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        request.Content = JsonContent.Create(new ResolveRequest(marketCode));

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
            catch (JsonException)
            {
                return response.IsSuccessStatusCode
                    ? Fail(503, "REFERENCE_PROVIDER_CONTRACT_INVALID")
                    : MapFailure(response.StatusCode, null);
            }

            if (response.IsSuccessStatusCode
                && envelope?.IsSuccessful == true
                && IsTrustedEvidence(envelope.Data?.Market, marketCode))
            {
                var selection = envelope.Data!.Market!;
                return VerifiedMarketReferenceResolveResult.Success(new VerifiedMarketReferenceSelection(
                    selection.SetCode,
                    selection.ValueCode,
                    selection.CatalogVersionId,
                    selection.CatalogVersionNumber,
                    selection.ResolutionMode,
                    selection.ResolvedAtUtc));
            }

            if (response.IsSuccessStatusCode)
            {
                return Fail(503, "REFERENCE_PROVIDER_CONTRACT_INVALID");
            }

            return MapFailure(response.StatusCode, envelope?.ReasonCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(504, "REFERENCE_PROVIDER_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
    }

    public async Task<VerifiedMarketEnumerationResult> EnumerateActiveAsync(
        CancellationToken cancellationToken = default)
    {
        if (!TryCreateAuthenticatedRequest(EnumerateActiveRelativePath, out var request, out var failureCode))
        {
            return VerifiedMarketEnumerationResult.Fail(503, failureCode!);
        }

        using (var authenticatedRequest = request!)
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            budget.CancelAfter(_options.Timeout);
            try
            {
                using var response = await _httpClient.SendAsync(
                    authenticatedRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    budget.Token);
                EnumerationEnvelope? envelope;
                try
                {
                    envelope = await response.Content.ReadFromJsonAsync<EnumerationEnvelope>(
                        cancellationToken: budget.Token);
                }
                catch (JsonException)
                {
                    return response.IsSuccessStatusCode
                        ? VerifiedMarketEnumerationResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH")
                        : MapEnumerationFailure(response.StatusCode, null);
                }

                if (response.IsSuccessStatusCode
                    && envelope?.IsSuccessful == true
                    && IsTrustedEnumeration(envelope.Data?.Markets))
                {
                    return VerifiedMarketEnumerationResult.Success(
                        envelope.Data!.Markets!
                            .Select(x => new VerifiedMarketOption(x.Code, x.DisplayText, x.SortOrder))
                            .ToList());
                }

                if (response.IsSuccessStatusCode)
                {
                    return VerifiedMarketEnumerationResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH");
                }

                return MapEnumerationFailure(response.StatusCode, envelope?.ReasonCode);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return VerifiedMarketEnumerationResult.Fail(504, "REFERENCE_PROVIDER_TIMEOUT");
            }
            catch (HttpRequestException)
            {
                return VerifiedMarketEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
            }
        }
    }

    private bool IsConfigured() =>
        _options.PlatformBaseAddress is { IsAbsoluteUri: true }
        && _options.Timeout > TimeSpan.Zero
        && _options.Timeout <= TimeSpan.FromSeconds(2)
        && !string.IsNullOrWhiteSpace(_options.CredentialIdentifier)
        && !string.IsNullOrEmpty(_options.CredentialSecret);

    private bool TryCreateAuthenticatedRequest(
        string relativePath,
        out HttpRequestMessage? request,
        out string? failureCode)
    {
        request = null;
        failureCode = null;
        if (!IsConfigured())
        {
            failureCode = "REFERENCE_PROVIDER_CONFIGURATION_INVALID";
            return false;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var inboundAuthorization = httpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (httpContext?.User.Identity?.IsAuthenticated != true
            || !AuthenticationHeaderValue.TryParse(inboundAuthorization, out var delegated)
            || !string.Equals(delegated.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(delegated.Parameter))
        {
            failureCode = "REFERENCE_PROVIDER_UNAVAILABLE";
            return false;
        }

        request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.PlatformBaseAddress!, relativePath));
        request.Headers.Authorization = delegated;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        return true;
    }

    private static bool IsTrustedEvidence(ResolveSelection? selection, string requestedMarketCode) =>
        selection is not null
        && string.Equals(selection.SetCode, "market", StringComparison.Ordinal)
        && string.Equals(selection.ValueCode, requestedMarketCode, StringComparison.Ordinal)
        && selection.CatalogVersionId != Guid.Empty
        && selection.CatalogVersionNumber > 0
        && string.Equals(selection.ResolutionMode, "LATEST", StringComparison.Ordinal)
        && selection.ResolvedAtUtc != default;

    private static VerifiedMarketReferenceResolveResult MapFailure(HttpStatusCode status, string? reasonCode) =>
        status switch
        {
            HttpStatusCode.NotFound => Fail(404, TrustedReason(reasonCode, "REFERENCE_MARKET_NOT_FOUND")),
            HttpStatusCode.GatewayTimeout => Fail(504, "REFERENCE_PROVIDER_TIMEOUT"),
            _ => Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
        };

    private static VerifiedMarketEnumerationResult MapEnumerationFailure(
        HttpStatusCode status,
        string? reasonCode) =>
        status switch
        {
            HttpStatusCode.NotFound => VerifiedMarketEnumerationResult.Fail(
                404,
                TrustedReason(reasonCode, "REFERENCE_MARKET_NOT_FOUND")),
            HttpStatusCode.GatewayTimeout => VerifiedMarketEnumerationResult.Fail(
                504,
                "REFERENCE_PROVIDER_TIMEOUT"),
            _ => VerifiedMarketEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
        };

    private static bool IsTrustedEnumeration(IReadOnlyList<EnumerationItem>? markets) =>
        markets is { Count: > 0 and <= 300 }
        && markets.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == markets.Count
        && markets.All(x =>
            IsExactAlpha2(x.Code)
            && !string.IsNullOrWhiteSpace(x.DisplayText)
            && x.SortOrder >= 0
            && x.AdditionalFields is null);

    private static bool IsExactAlpha2(string? value) =>
        value is { Length: 2 }
        && value[0] is >= 'A' and <= 'Z'
        && value[1] is >= 'A' and <= 'Z';

    private static string TrustedReason(string? reasonCode, string fallback) =>
        !string.IsNullOrWhiteSpace(reasonCode)
        && reasonCode.StartsWith("REFERENCE_", StringComparison.Ordinal)
            ? reasonCode
            : fallback;

    private static VerifiedMarketReferenceResolveResult Fail(int statusCode, string code) =>
        VerifiedMarketReferenceResolveResult.Fail(statusCode, code);

    private sealed record ResolveRequest(
        [property: JsonPropertyName("market_code")] string MarketCode);

    private sealed class ResolveEnvelope
    {
        public bool IsSuccessful { get; init; }
        public ResolveData? Data { get; init; }

        [JsonPropertyName("reason_code")]
        public string? ReasonCode { get; init; }
    }

    private sealed record ResolveData(
        [property: JsonPropertyName("market")] ResolveSelection? Market);

    private sealed record ResolveSelection(
        [property: JsonPropertyName("set_code")] string SetCode,
        [property: JsonPropertyName("value_code")] string ValueCode,
        [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
        [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
        [property: JsonPropertyName("resolution_mode")] string ResolutionMode,
        [property: JsonPropertyName("resolved_at_utc")] DateTimeOffset ResolvedAtUtc);

    private sealed class EnumerationEnvelope
    {
        public bool IsSuccessful { get; init; }
        public EnumerationData? Data { get; init; }

        [JsonPropertyName("reason_code")]
        public string? ReasonCode { get; init; }
    }

    private sealed record EnumerationData(
        [property: JsonPropertyName("markets")] IReadOnlyList<EnumerationItem>? Markets);

    private sealed class EnumerationItem
    {
        [JsonPropertyName("code")]
        public string Code { get; init; } = string.Empty;

        [JsonPropertyName("display_text")]
        public string DisplayText { get; init; } = string.Empty;

        [JsonPropertyName("sort_order")]
        public int SortOrder { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; init; }
    }
}
