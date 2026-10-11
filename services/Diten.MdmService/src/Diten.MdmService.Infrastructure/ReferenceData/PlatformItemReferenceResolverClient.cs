using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.ReferenceData;

/// <summary>
/// WP-PLT-ITEM-LISTS-01: the item lists (MOD-0048 ITEM-UNIVERSAL-Vn) from Platform's
/// <c>POST api/internal/v1/reference-data/item-universal/resolve</c>. The PlatformVerifiedGskuResolverClient trust pattern:
/// the MDM resolver credential + audience headers and the caller's own tenant JWT, a 2-second budget, fail-closed.
/// An answer that does not exactly cover the request — a pair missing or extra, a version that disagrees inside a set, a
/// storage condition without its ConditionKind, a unit without its dimension — is UNTRUSTED_ANSWER (503 at the item).
/// </summary>
public sealed class PlatformItemReferenceResolverClient : IItemReferenceResolver
{
    public const string RelativePath = "api/internal/v1/reference-data/item-universal/resolve";
    // MOD-0290-FU04 S5b (BL-600): the same controller, the same trust (credential, audience, caller's token).
    public const string EnumerateRelativePath = "api/internal/v1/reference-data/item-universal/enumerate";
    public const string CredentialIdHeader = "X-Verified-Gsku-Credential-Id";
    public const string CredentialSecretHeader = "X-Verified-Gsku-Credential";
    public const string AudienceHeader = "X-Verified-Gsku-Audience";
    public const string Audience = "VERIFIED_GSKU_RESOLVE";
    public const int MaximumSelections = 200;
    public static readonly TimeSpan MaximumBudget = TimeSpan.FromSeconds(2);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly VerifiedGskuResolverOptions _options;
    private readonly ILogger<PlatformItemReferenceResolverClient> _logger;
    private readonly bool _development;

    public PlatformItemReferenceResolverClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor,
        IOptions<VerifiedGskuResolverOptions> options, ILogger<PlatformItemReferenceResolverClient> logger,
        IHostEnvironment? environment = null)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
        // No environment known: the strict rule (https only).
        _development = environment?.IsDevelopment() == true;
    }

    public const string ConfigurationInvalid = "REFERENCE_PROVIDER_CONFIGURATION_INVALID";

    // FIX1 M4: outside Development the base must be https — the credential secret and the caller's token never travel
    // as plain text.
    public static bool IsConfigured(VerifiedGskuResolverOptions? options, bool development) =>
        options?.PlatformBaseAddress is { IsAbsoluteUri: true } address
        && (address.Scheme == Uri.UriSchemeHttps || development && address.Scheme == Uri.UriSchemeHttp)
        && options.Timeout > TimeSpan.Zero && options.Timeout <= MaximumBudget
        && !string.IsNullOrWhiteSpace(options.CredentialIdentifier)
        && !string.IsNullOrEmpty(options.CredentialSecret);

    public async Task<ItemReferenceResolveResult> ResolveLatestAsync(IReadOnlyList<ItemReferenceRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(_options, _development))
        {
            // CT decision A: fail-closed at the call, never "no list, pass" — and said in the log every time.
            _logger.LogWarning(ItemReferenceProvider.NotConfiguredWarning);
            return ItemReferenceResolveResult.Fail(503, ConfigurationInvalid);
        }
        var wanted = (requests ?? []).Where(request => request is not null).Distinct().ToList();
        if (wanted.Count is 0 or > MaximumSelections)
            return ItemReferenceResolveResult.Fail(503, "REFERENCE_BATCH_LIMIT_EXCEEDED");

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true
            || !AuthenticationHeaderValue.TryParse(httpContext.Request.Headers.Authorization.FirstOrDefault(), out var delegated)
            || !string.Equals(delegated.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(delegated.Parameter))
            return ItemReferenceResolveResult.Fail(401, "REFERENCE_UNAUTHENTICATED");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.PlatformBaseAddress!, RelativePath));
        request.Headers.Authorization = delegated;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        request.Content = JsonContent.Create(new ResolveRequest(
            wanted.Select(item => new ResolveSelectionRequest(item.SetCode, item.ValueCode, "LATEST")).ToList()));

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            Envelope? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<Envelope>(cancellationToken: budget.Token);
            }
            catch (System.Text.Json.JsonException)
            {
                return (int)response.StatusCode >= 500
                    ? ItemReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
                    : ItemReferenceResolveResult.Fail(503, ItemReferenceCheck.UntrustedAnswer);
            }

            if (response.IsSuccessStatusCode)
            {
                return envelope?.IsSuccessful == true && envelope.Data?.Selections is { } selections
                       && Trusted(wanted, selections)
                    ? ItemReferenceResolveResult.Success(selections.Select(Map).ToList())
                    : ItemReferenceResolveResult.Fail(503, ItemReferenceCheck.UntrustedAnswer);
            }

            if (response.StatusCode == HttpStatusCode.NotFound
                && string.Equals(envelope?.ReasonCode, ItemReferenceResolveResult.ValueNotFound, StringComparison.Ordinal))
            {
                // Named: every missing pair is one we asked for — otherwise the answer is not trusted.
                var missing = envelope.Data?.NotFound;
                return missing is { Count: > 0 } && missing.All(pair => pair is not null
                           && wanted.Contains(new ItemReferenceRequest(pair.SetCode, pair.ValueCode)))
                    ? ItemReferenceResolveResult.Missing(
                        missing.Select(pair => new ItemReferenceRequest(pair!.SetCode, pair.ValueCode)).ToList())
                    : ItemReferenceResolveResult.Fail(503, ItemReferenceCheck.UntrustedAnswer);
            }

            return MapFailure(response.StatusCode, envelope?.ReasonCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ItemReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return ItemReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // FIX1 M2: anything else (an HTML error page, an unreadable body) is an outage by name — the type only in the
            // log, never a header, a token or the body.
            _logger.LogWarning("Item reference provider answer could not be read ({ExceptionType}).", exception.GetType().Name);
            return ItemReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
    }

    /// <summary>
    /// MOD-0290-FU04 S5b (BL-600): every value of the asked sets. The resolve's preflight and trust (configuration,
    /// caller's token, credential headers, budget); an answer that does not list exactly the asked sets — once each, each
    /// with a version and at least one value, codes unique, the attributes the forms read present — is UNTRUSTED_ANSWER.
    /// Every failure is an outage (503, its code), never an empty list.
    /// </summary>
    public async Task<ItemReferenceEnumerationResult> EnumerateLatestAsync(IReadOnlyList<string> setCodes,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(_options, _development))
        {
            _logger.LogWarning(ItemReferenceProvider.NotConfiguredWarning);
            return ItemReferenceEnumerationResult.Fail(503, ConfigurationInvalid);
        }
        var wanted = (setCodes ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count is 0 or > 3) return ItemReferenceEnumerationResult.Fail(503, "REFERENCE_RESOLUTION_CONTRACT_INVALID");

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true
            || !AuthenticationHeaderValue.TryParse(httpContext.Request.Headers.Authorization.FirstOrDefault(), out var delegated)
            || !string.Equals(delegated.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(delegated.Parameter))
            return ItemReferenceEnumerationResult.Fail(401, "REFERENCE_UNAUTHENTICATED");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.PlatformBaseAddress!, EnumerateRelativePath));
        request.Headers.Authorization = delegated;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        request.Content = JsonContent.Create(new EnumerateRequest(wanted));

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            EnumerationEnvelope? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<EnumerationEnvelope>(cancellationToken: budget.Token);
            }
            catch (System.Text.Json.JsonException)
            {
                return (int)response.StatusCode >= 500
                    ? ItemReferenceEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
                    : ItemReferenceEnumerationResult.Fail(503, ItemReferenceCheck.UntrustedAnswer);
            }

            if (response.IsSuccessStatusCode)
            {
                return envelope?.IsSuccessful == true && envelope.Data?.Sets is { } sets && TrustedEnumeration(wanted, sets)
                    ? ItemReferenceEnumerationResult.Success(sets.Select(set => new ItemReferenceEnumeratedSet(
                        set!.SetCode, set.CatalogVersion, set.CatalogVersionId, set.CatalogVersionNumber,
                        set.Values!.Select(value => new ItemReferenceEnumeratedValue(value!.ValueCode, value.DisplayName,
                            value.SortOrder, value.Attributes!)).ToList())).ToList())
                    : ItemReferenceEnumerationResult.Fail(503, ItemReferenceCheck.UntrustedAnswer);
            }

            var failure = MapFailure(response.StatusCode, envelope?.ReasonCode);
            return ItemReferenceEnumerationResult.Fail(failure.StatusCode, failure.FailureCode!);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ItemReferenceEnumerationResult.Fail(503, "REFERENCE_PROVIDER_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return ItemReferenceEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Item reference provider answer could not be read ({ExceptionType}).", exception.GetType().Name);
            return ItemReferenceEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
    }

    internal static bool TrustedEnumeration(IReadOnlyList<string> wanted, IReadOnlyList<EnumeratedSet?> sets) =>
        sets.Count == wanted.Count
        && sets.All(set => set is not null)
        && sets.Select(set => set!.SetCode).ToHashSet(StringComparer.Ordinal).SetEquals(wanted)
        && sets.All(set => set!.CatalogVersionId != Guid.Empty && !string.IsNullOrWhiteSpace(set.CatalogVersion)
                           && set.CatalogVersionNumber > 0
                           && set.Values is { Count: > 0 } values
                           && values.All(value => value is not null && !string.IsNullOrWhiteSpace(value.ValueCode)
                                                  && !string.IsNullOrWhiteSpace(value.DisplayName) && value.Attributes is not null
                                                  && AttributesHold(set.SetCode, value.Attributes))
                           && values.Select(value => value!.ValueCode).Distinct(StringComparer.Ordinal).Count() == values.Count);

    // The answer covers exactly the request, once each; versions agree inside a set; the attributes the item reads exist.
    internal static bool Trusted(IReadOnlyList<ItemReferenceRequest> wanted, IReadOnlyList<Selection?> selections)
    {
        if (selections.Count != wanted.Count || selections.Any(selection => selection is null)) return false;
        var answered = selections.Select(selection => new ItemReferenceRequest(selection!.SetCode, selection.ValueCode)).ToList();
        // With Count == wanted.Count and SetEquals(wanted), a repeated pair is already impossible (CT L1: equivalent);
        // the Distinct check stays as the explicit statement of the rule "each pair once".
        if (answered.Distinct().Count() != answered.Count || !answered.ToHashSet().SetEquals(wanted)) return false;
        if (selections.GroupBy(selection => selection!.SetCode)
            .Any(set => set.Select(selection => (selection!.CatalogVersion, selection.CatalogVersionId, selection.CatalogVersionNumber))
                .Distinct().Count() != 1)) return false;
        return selections.All(selection => selection!.CatalogVersionId != Guid.Empty
                                           && !string.IsNullOrWhiteSpace(selection.CatalogVersion)
                                           && selection.CatalogVersionNumber > 0
                                           && string.Equals(selection.ResolutionMode, "LATEST", StringComparison.Ordinal)
                                           && selection.ResolvedAtUtc != default
                                           && selection.Attributes is not null
                                           && AttributesHold(selection.SetCode, selection.Attributes));
    }

    // One rule for the resolve and the enumeration: the attributes the item and the forms read.
    private static bool AttributesHold(string setCode, IReadOnlyDictionary<string, string?> attributes) => setCode switch
    {
        ItemReferenceSets.StorageCondition =>
            attributes.TryGetValue(ItemReferenceSets.ConditionKindAttribute, out var kind)
            && kind is ItemReferenceSets.TemperatureKind or ItemReferenceSets.ProtectionKind,
        ItemReferenceSets.ItemUom =>
            attributes.TryGetValue("DimensionCode", out var dimension) && !string.IsNullOrWhiteSpace(dimension)
            && attributes.TryGetValue("MaximumDecimalPrecision", out var precision)
            && int.TryParse(precision, out var digits) && digits >= 0,
        ItemReferenceSets.MaterialType => true,
        _ => false
    };

    private static ItemReferenceSelection Map(Selection? selection) => new(
        selection!.SetCode, selection.ValueCode, selection.CatalogVersionId, selection.CatalogVersionNumber,
        selection.ResolutionMode, selection.ResolvedAtUtc, selection.IsRetired, selection.SelectableForNew,
        selection.Attributes!);

    private static ItemReferenceResolveResult MapFailure(HttpStatusCode status, string? reasonCode)
    {
        var fallback = (int)status switch
        {
            400 => "REFERENCE_RESOLUTION_CONTRACT_INVALID",
            401 => "REFERENCE_UNAUTHENTICATED",
            403 => "REFERENCE_FORBIDDEN",
            504 => "REFERENCE_PROVIDER_TIMEOUT",
            _ => "REFERENCE_PROVIDER_UNAVAILABLE"
        };
        var code = reasonCode is not null && reasonCode.StartsWith("REFERENCE_", StringComparison.Ordinal) ? reasonCode : fallback;
        // Every failure is an outage for the item (fail-closed): nothing here is the user's fault.
        return ItemReferenceResolveResult.Fail(503, code);
    }

    private sealed record EnumerateRequest([property: JsonPropertyName("set_codes")] IReadOnlyList<string> SetCodes);

    private sealed class EnumerationEnvelope
    {
        public bool IsSuccessful { get; init; }
        public EnumerationData? Data { get; init; }

        [JsonPropertyName("reason_code")]
        public string? ReasonCode { get; init; }
    }

    private sealed record EnumerationData([property: JsonPropertyName("sets")] IReadOnlyList<EnumeratedSet?>? Sets);

    internal sealed record EnumeratedSet(
        [property: JsonPropertyName("set_code")] string SetCode,
        [property: JsonPropertyName("catalog_version")] string CatalogVersion,
        [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
        [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
        [property: JsonPropertyName("values")] IReadOnlyList<EnumeratedValue?>? Values);

    internal sealed record EnumeratedValue(
        [property: JsonPropertyName("value_code")] string ValueCode,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("sort_order")] int SortOrder,
        [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string?>? Attributes);

    private sealed record ResolveRequest([property: JsonPropertyName("selections")] IReadOnlyList<ResolveSelectionRequest> Selections);

    private sealed record ResolveSelectionRequest(
        [property: JsonPropertyName("set_code")] string SetCode,
        [property: JsonPropertyName("value_code")] string ValueCode,
        [property: JsonPropertyName("resolution_mode")] string ResolutionMode);

    private sealed class Envelope
    {
        public bool IsSuccessful { get; init; }
        public Data? Data { get; init; }

        [JsonPropertyName("reason_code")]
        public string? ReasonCode { get; init; }
    }

    private sealed record Data(
        [property: JsonPropertyName("selections")] IReadOnlyList<Selection?>? Selections,
        [property: JsonPropertyName("not_found")] IReadOnlyList<NotFoundPair?>? NotFound);

    internal sealed record Selection(
        [property: JsonPropertyName("set_code")] string SetCode,
        [property: JsonPropertyName("value_code")] string ValueCode,
        [property: JsonPropertyName("catalog_version")] string CatalogVersion,
        [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
        [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
        [property: JsonPropertyName("resolution_mode")] string ResolutionMode,
        [property: JsonPropertyName("resolved_at_utc")] DateTimeOffset ResolvedAtUtc,
        [property: JsonPropertyName("is_retired")] bool IsRetired,
        [property: JsonPropertyName("selectable_for_new")] bool SelectableForNew,
        [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string?>? Attributes);

    private sealed record NotFoundPair(
        [property: JsonPropertyName("set_code")] string SetCode,
        [property: JsonPropertyName("value_code")] string ValueCode);
}
