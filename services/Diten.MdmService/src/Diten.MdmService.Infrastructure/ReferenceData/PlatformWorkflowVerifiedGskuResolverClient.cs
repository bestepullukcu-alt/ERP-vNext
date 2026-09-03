using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.ReferenceData;

public sealed class PlatformWorkflowVerifiedGskuResolverClient : IWorkflowVerifiedGskuReferenceResolver
{
    public const string CredentialIdHeader = "X-Verified-Gsku-Credential-Id";
    public const string CredentialSecretHeader = "X-Verified-Gsku-Credential";
    public const string AudienceHeader = "X-Verified-Gsku-Audience";
    public const string ResolverAudience = "VERIFIED_GSKU_RESOLVE";
    private const string ResolvePath = "/api/internal/v1/reference-data/verified-gsku/resolve";
    private const int MaximumResponseBytes = 32 * 1024;
    private static readonly TimeSpan MaximumBudget = TimeSpan.FromSeconds(2);
    private readonly IHttpClientFactory _clients;
    private readonly IWorkflowVerifiedMarketServiceIdentityProvider _identities;
    private readonly VerifiedGskuResolverOptions _options;

    public PlatformWorkflowVerifiedGskuResolverClient(
        IHttpClientFactory clients,
        IWorkflowVerifiedMarketServiceIdentityProvider identities,
        IOptions<VerifiedGskuResolverOptions> options)
    {
        _clients = clients;
        _identities = identities;
        _options = options.Value;
    }

    public async Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
        Guid tenantId,
        string packApplicabilityValueCode,
        string uomValueCode,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty
            || !IsAllowedPackApplicability(packApplicabilityValueCode)
            || !IsAllowedUom(uomValueCode)
            || !IsConfigured())
        {
            return Fail(503, "REFERENCE_PROVIDER_CONFIGURATION_INVALID");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.Timeout);
        try
        {
            var identity = await _identities.GetAsync(tenantId, false, budget.Token);
            var first = await SendOnceAsync(identity, packApplicabilityValueCode, uomValueCode, budget.Token);
            if (first.StatusCode != 401) return first.Result;

            identity = await _identities.GetAsync(tenantId, true, budget.Token);
            var replay = await SendOnceAsync(identity, packApplicabilityValueCode, uomValueCode, budget.Token);
            return replay.StatusCode == 401
                ? Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
                : replay.Result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(504, "REFERENCE_PROVIDER_TIMEOUT");
        }
        catch (WorkflowVerifiedMarketServiceIdentityException exception)
        {
            return exception.IsRetryable && exception.ErrorCode.EndsWith("TIMEOUT", StringComparison.Ordinal)
                ? Fail(504, "REFERENCE_PROVIDER_TIMEOUT")
                : Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
        catch (InvalidDataException)
        {
            return Contract();
        }
        catch (IOException)
        {
            return Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
    }

    private async Task<Attempt> SendOnceAsync(
        WorkflowVerifiedMarketServiceIdentity identity,
        string packApplicabilityValueCode,
        string uomValueCode,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.PlatformBaseAddress!, ResolvePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.AccessToken);
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, ResolverAudience);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            selections = new[]
            {
                new { set_code = "pack-applicability", value_code = packApplicabilityValueCode, resolution_mode = "LATEST" },
                new { set_code = "uom", value_code = uomValueCode, resolution_mode = "LATEST" }
            }
        }), Encoding.UTF8, "application/json");

        using var response = await _clients
            .CreateClient(nameof(PlatformWorkflowVerifiedGskuResolverClient))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new(401, Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"));
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return new((int)response.StatusCode, MapTransportFailure(response.StatusCode));

        var payload = await ReadBoundedAsync(response.Content, cancellationToken);
        return new((int)response.StatusCode, Parse(response.StatusCode, payload, packApplicabilityValueCode, uomValueCode));
    }

    private static VerifiedGskuReferenceResolveResult Parse(
        HttpStatusCode status,
        byte[] payload,
        string packApplicabilityValueCode,
        string uomValueCode)
    {
        try
        {
            using var document = JsonDocument.Parse(payload, StrictOptions);
            var root = document.RootElement;
            RequireExact(root, "data", "statusCode", "isSuccessful", "errors", "reason_code", "correlation_id");
            if (root.GetProperty("statusCode").GetInt32() != (int)status
                || root.GetProperty("isSuccessful").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || root.GetProperty("errors").ValueKind != JsonValueKind.Array
                || !IsOptionalExactCorrelation(root.GetProperty("correlation_id")))
                return Contract();

            if (status == HttpStatusCode.OK)
            {
                if (!root.GetProperty("isSuccessful").GetBoolean()
                    || root.GetProperty("errors").GetArrayLength() != 0
                    || root.GetProperty("reason_code").ValueKind != JsonValueKind.Null
                    || root.GetProperty("data").ValueKind != JsonValueKind.Object)
                    return Contract();
                var data = root.GetProperty("data");
                RequireExact(data, "selections");
                var values = data.GetProperty("selections");
                if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() != 2) return Contract();
                var selections = values.EnumerateArray().Select(ParseSelection).ToList();
                return Trusted(selections, packApplicabilityValueCode, uomValueCode)
                    ? VerifiedGskuReferenceResolveResult.Success(selections)
                    : Contract();
            }

            if (root.GetProperty("isSuccessful").GetBoolean()
                || root.GetProperty("errors").GetArrayLength() == 0
                || root.GetProperty("data").ValueKind != JsonValueKind.Null)
                return Contract();
            var reason = root.GetProperty("reason_code").ValueKind == JsonValueKind.String
                ? root.GetProperty("reason_code").GetString()
                : null;
            return IsExact(reason, 160) ? MapFailure(status, reason) : Contract();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return Contract();
        }
    }

    private static VerifiedGskuReferenceSelection ParseSelection(JsonElement item)
    {
        RequireExact(item, "set_code", "value_code", "catalog_version_id", "catalog_version_number",
            "resolution_mode", "resolved_at_utc", "is_retired", "selectable_for_new");
        return new(
            item.GetProperty("set_code").GetString()!, item.GetProperty("value_code").GetString()!,
            item.GetProperty("catalog_version_id").GetGuid(), item.GetProperty("catalog_version_number").GetInt32(),
            item.GetProperty("resolution_mode").GetString()!, item.GetProperty("resolved_at_utc").GetDateTimeOffset(),
            item.GetProperty("is_retired").GetBoolean(), item.GetProperty("selectable_for_new").GetBoolean());
    }

    private bool IsConfigured() => _options.PlatformBaseAddress is { IsAbsoluteUri: true } uri
        && uri.Scheme is "http" or "https" && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && _options.Timeout > TimeSpan.Zero && _options.Timeout <= MaximumBudget
        && IsExact(_options.CredentialIdentifier, 256) && IsExact(_options.CredentialSecret, 512);

    private static VerifiedGskuReferenceResolveResult MapFailure(HttpStatusCode status, string? reason) => status switch
    {
        HttpStatusCode.Forbidden => Fail(403, IsReferenceReason(reason) ? reason! : "REFERENCE_FORBIDDEN"),
        HttpStatusCode.NotFound => Fail(404, IsReferenceReason(reason) ? reason! : "REFERENCE_SET_NOT_ACCESSIBLE"),
        HttpStatusCode.Conflict => Fail(409, IsReferenceReason(reason) ? reason! : "REFERENCE_RESOLUTION_CONTRACT_INVALID"),
        HttpStatusCode.GatewayTimeout => Fail(504, "REFERENCE_PROVIDER_TIMEOUT"),
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable =>
            Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
        _ when (int)status >= 500 => Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
        _ => Contract()
    };

    private static VerifiedGskuReferenceResolveResult MapTransportFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Forbidden => Fail(403, "REFERENCE_FORBIDDEN"),
        HttpStatusCode.NotFound => Fail(404, "REFERENCE_SET_NOT_ACCESSIBLE"),
        HttpStatusCode.Conflict => Fail(409, "REFERENCE_RESOLUTION_CONTRACT_INVALID"),
        HttpStatusCode.GatewayTimeout => Fail(504, "REFERENCE_PROVIDER_TIMEOUT"),
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable =>
            Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
        _ when (int)status >= 500 => Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
        _ => Contract()
    };

    private static bool Trusted(IReadOnlyList<VerifiedGskuReferenceSelection> selections, string pack, string uom) =>
        selections.Select(item => item.SetCode).Distinct(StringComparer.Ordinal).Count() == 2
        && selections.All(item => item.CatalogVersionId != Guid.Empty && item.CatalogVersionNumber > 0
            && item.ResolutionMode == "LATEST" && item.ResolvedAtUtc != default
            && item.ResolvedAtUtc.Offset == TimeSpan.Zero && !item.IsRetired && item.SelectableForNew)
        && selections.Any(item => item.SetCode == "pack-applicability" && item.ValueCode == pack)
        && selections.Any(item => item.SetCode == "uom" && item.ValueCode == uom);

    private static bool IsAllowedPackApplicability(string? value) => value == "SCALAR_QUANTITY_APPLIES";
    private static bool IsAllowedUom(string? value) => value is "C62" or "GRM" or "KGM" or "MLT" or "LTR";
    private static bool IsReferenceReason(string? value) => IsExact(value, 160) && value!.StartsWith("REFERENCE_", StringComparison.Ordinal);
    private static bool IsOptionalExactCorrelation(JsonElement value) => value.ValueKind == JsonValueKind.Null
        || value.ValueKind == JsonValueKind.String && IsExact(value.GetString(), 256);
    private static bool IsExact(string? value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum && value == value.Trim() && !value.Any(char.IsControl) && !value.Contains(',');

    private static void RequireExact(JsonElement element, params string[] names)
    {
        var found = element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Select(property => property.Name).ToArray()
            : [];
        if (found.Length != names.Length || found.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(name => !found.Contains(name, StringComparer.Ordinal)))
            throw new JsonException();
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes) throw new InvalidDataException();
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[2048];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) return output.ToArray();
            if (output.Length + read > MaximumResponseBytes) throw new InvalidDataException();
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static VerifiedGskuReferenceResolveResult Contract() => Fail(409, "REFERENCE_CONTRACT_MISMATCH");
    private static VerifiedGskuReferenceResolveResult Fail(int statusCode, string code) =>
        VerifiedGskuReferenceResolveResult.Fail(statusCode, code);
    private static readonly JsonDocumentOptions StrictOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8
    };
    private sealed record Attempt(int StatusCode, VerifiedGskuReferenceResolveResult Result);
}
