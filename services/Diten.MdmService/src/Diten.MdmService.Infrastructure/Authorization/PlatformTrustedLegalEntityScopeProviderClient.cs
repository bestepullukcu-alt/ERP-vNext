using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.Authorization;

public sealed class PlatformTrustedLegalEntityScopeProviderClient : ITrustedLegalEntityScopeProvider
{
    internal const string CredentialIdHeader = "X-Legal-Entity-Scope-Credential-Id";
    internal const string CredentialSecretHeader = "X-Legal-Entity-Scope-Credential";
    internal const string AudienceHeader = "X-Legal-Entity-Scope-Audience";
    internal const string Audience = "TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE";

    private const string ModuleCode = "product-item-sku-master";
    private const string RelativePath = "api/internal/v1/access-governance/legal-entity-scope/resolve";
    private const int MaximumResponseBytes = 32_768;
    private const int MaximumCandidates = 200;
    private const int MaximumPermissionKeyLength = 200;

    private static readonly HashSet<string> EnvelopeProperties = new(StringComparer.Ordinal)
    {
        "data", "statusCode", "isSuccessful", "errors", "reason_code", "correlation_id"
    };

    private static readonly HashSet<string> DataProperties = new(StringComparer.Ordinal)
    {
        "tenantId", "subjectId", "moduleCode", "permissionKey", "evaluatedAtUtc", "legalEntityIds"
    };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TrustedLegalEntityScopeProviderOptions _options;
    private readonly TimeProvider _clock;

    public PlatformTrustedLegalEntityScopeProviderClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        IOptions<TrustedLegalEntityScopeProviderOptions> options,
        TimeProvider clock)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
        Guid expectedTenantId,
        Guid expectedSubjectId,
        string moduleCode,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsValidRequest(expectedTenantId, expectedSubjectId, moduleCode, permissionKey))
        {
            return Fail(400, "LEGAL_ENTITY_SCOPE_REQUEST_INVALID");
        }

        if (!TryGetDelegatedBearer(out var delegatedBearer))
        {
            return Fail(401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED");
        }

        if (!IsConfigured())
        {
            return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_CONFIGURATION_INVALID");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.PlatformBaseAddress!, RelativePath));
        request.Headers.Authorization = delegatedBearer;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        request.Content = new StringContent(
            CreateRequestBody(moduleCode, permissionKey),
            Encoding.UTF8,
            "application/json");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                budget.Token);

            if (!IsJson(response.Content.Headers.ContentType))
            {
                return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID");
            }

            var payload = await ReadBoundedAsync(response.Content, budget.Token);
            if (payload is null)
            {
                return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID");
            }

            return ParseAndMap(
                response.StatusCode,
                payload,
                expectedTenantId,
                expectedSubjectId,
                moduleCode,
                permissionKey,
                _clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(504, "LEGAL_ENTITY_SCOPE_PROVIDER_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE");
        }
        catch (IOException)
        {
            return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE");
        }
    }

    private bool IsConfigured() =>
        _options.PlatformBaseAddress is { IsAbsoluteUri: true } baseAddress
        && IsSecureInternalUri(baseAddress)
        && _options.Timeout > TimeSpan.Zero
        && _options.Timeout <= TimeSpan.FromSeconds(2)
        && !string.IsNullOrWhiteSpace(_options.CredentialIdentifier)
        && !string.IsNullOrEmpty(_options.CredentialSecret);

    private static bool IsSecureInternalUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;

    private bool TryGetDelegatedBearer(out AuthenticationHeaderValue? delegatedBearer)
    {
        delegatedBearer = null;
        var context = _httpContextAccessor.HttpContext;
        var values = context?.Request.Headers.Authorization;
        if (context?.User.Identity?.IsAuthenticated != true || values is null || values.Value.Count != 1)
        {
            return false;
        }

        if (!AuthenticationHeaderValue.TryParse(values.Value[0], out var parsed)
            || !string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            return false;
        }

        delegatedBearer = parsed;
        return true;
    }

    private static bool IsValidRequest(
        Guid tenantId,
        Guid subjectId,
        string moduleCode,
        string permissionKey) =>
        tenantId != Guid.Empty
        && subjectId != Guid.Empty
        && string.Equals(moduleCode, ModuleCode, StringComparison.Ordinal)
        && IsValidPermissionKey(permissionKey);

    private static bool IsValidPermissionKey(string? value)
    {
        if (value is null || value.Length is < 1 or > MaximumPermissionKeyLength)
        {
            return false;
        }

        var segments = value.Split('.');
        return segments.Length >= 3 && segments.All(IsValidKebabSegment);
    }

    private static bool IsValidKebabSegment(string value)
    {
        if (value.Length == 0 || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var character in value)
        {
            var isLowerAlphaNumeric = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (!isLowerAlphaNumeric && character != '-')
            {
                return false;
            }
            if (character == '-' && previousHyphen)
            {
                return false;
            }

            previousHyphen = character == '-';
        }

        return true;
    }

    private static string CreateRequestBody(string moduleCode, string permissionKey)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("module_code", moduleCode);
            writer.WriteString("permission_key", permissionKey);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool IsJson(MediaTypeHeaderValue? contentType) =>
        contentType?.MediaType is { } mediaType
        && (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes)
        {
            return null;
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumResponseBytes + 1];
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(bytesRead, buffer.Length - bytesRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        return bytesRead is > 0 and <= MaximumResponseBytes
            ? buffer[..bytesRead]
            : null;
    }

    private static TrustedLegalEntityScopeProviderResult ParseAndMap(
        HttpStatusCode httpStatus,
        byte[] payload,
        Guid expectedTenantId,
        Guid expectedSubjectId,
        string expectedModuleCode,
        string expectedPermissionKey,
        DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!TryReadEnvelope(document.RootElement, out var envelope)
                || envelope.StatusCode != (int)httpStatus)
            {
                return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID");
            }

            if (httpStatus == HttpStatusCode.OK)
            {
                return envelope.IsSuccessful
                    && envelope.Errors.Count == 0
                    && envelope.Data.HasValue
                    && TryReadSuccess(
                        envelope.Data.Value,
                        expectedTenantId,
                        expectedSubjectId,
                        expectedModuleCode,
                        expectedPermissionKey,
                        now,
                        out var result)
                        ? result!
                        : Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID");
            }

            if (envelope.IsSuccessful
                || envelope.Errors.Count == 0
                || envelope.Data is { ValueKind: not JsonValueKind.Null })
            {
                return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID");
            }

            return httpStatus switch
            {
                HttpStatusCode.BadRequest => Fail(400, "LEGAL_ENTITY_SCOPE_REQUEST_INVALID"),
                HttpStatusCode.Unauthorized => Fail(401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED"),
                HttpStatusCode.Forbidden => Fail(403, "LEGAL_ENTITY_SCOPE_FORBIDDEN"),
                HttpStatusCode.GatewayTimeout => Fail(504, "LEGAL_ENTITY_SCOPE_PROVIDER_TIMEOUT"),
                _ => Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")
            };
        }
        catch (JsonException)
        {
            return Fail(503, "LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID");
        }
    }

    private static bool TryReadEnvelope(JsonElement root, out ParsedEnvelope envelope)
    {
        envelope = default;
        if (root.ValueKind != JsonValueKind.Object
            || !HasExactProperties(root, EnvelopeProperties))
        {
            return false;
        }

        var statusCode = root.GetProperty("statusCode");
        var isSuccessful = root.GetProperty("isSuccessful");
        var errors = root.GetProperty("errors");
        if (statusCode.ValueKind != JsonValueKind.Number
            || !statusCode.TryGetInt32(out var parsedStatus)
            || isSuccessful.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || errors.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsedErrors = new List<string>();
        foreach (var error in errors.EnumerateArray())
        {
            if (error.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(error.GetString()))
            {
                return false;
            }

            parsedErrors.Add(error.GetString()!);
        }

        var reasonCode = root.GetProperty("reason_code");
        var correlationId = root.GetProperty("correlation_id");
        if (!IsNullOrString(reasonCode) || !IsNullOrString(correlationId))
        {
            return false;
        }

        envelope = new ParsedEnvelope(
            parsedStatus,
            isSuccessful.GetBoolean(),
            parsedErrors,
            root.GetProperty("data"));
        return true;
    }

    private static bool TryReadSuccess(
        JsonElement data,
        Guid expectedTenantId,
        Guid expectedSubjectId,
        string expectedModuleCode,
        string expectedPermissionKey,
        DateTimeOffset now,
        out TrustedLegalEntityScopeProviderResult? result)
    {
        result = null;
        if (data.ValueKind != JsonValueKind.Object || !HasExactProperties(data, DataProperties))
        {
            return false;
        }

        if (!TryReadGuid(data.GetProperty("tenantId"), out var tenantId)
            || tenantId != expectedTenantId
            || !TryReadGuid(data.GetProperty("subjectId"), out var subjectId)
            || subjectId != expectedSubjectId
            || !TryReadExactString(data.GetProperty("moduleCode"), expectedModuleCode)
            || !TryReadExactString(data.GetProperty("permissionKey"), expectedPermissionKey)
            || !TryReadUtc(data.GetProperty("evaluatedAtUtc"), out var evaluatedAtUtc)
            || evaluatedAtUtc < now.AddMinutes(-5)
            || evaluatedAtUtc > now.AddMinutes(1))
        {
            return false;
        }

        var idsElement = data.GetProperty("legalEntityIds");
        if (idsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var ids = new List<Guid>();
        foreach (var element in idsElement.EnumerateArray())
        {
            if (ids.Count == MaximumCandidates || !TryReadGuid(element, out var id) || id == Guid.Empty)
            {
                return false;
            }

            ids.Add(id);
        }

        if (ids.Distinct().Count() != ids.Count
            || !ids.SequenceEqual(ids.OrderBy(id => id.ToString("D"), StringComparer.Ordinal)))
        {
            return false;
        }

        result = TrustedLegalEntityScopeProviderResult.Success(
            tenantId,
            subjectId,
            expectedModuleCode,
            expectedPermissionKey,
            evaluatedAtUtc,
            ids);
        return true;
    }

    private static bool HasExactProperties(JsonElement element, IReadOnlySet<string> expected)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name) || !expected.Contains(property.Name))
            {
                return false;
            }
        }

        return names.SetEquals(expected);
    }

    private static bool TryReadGuid(JsonElement element, out Guid value)
    {
        value = Guid.Empty;
        return element.ValueKind == JsonValueKind.String
            && Guid.TryParseExact(element.GetString(), "D", out value);
    }

    private static bool TryReadExactString(JsonElement element, string expected) =>
        element.ValueKind == JsonValueKind.String
        && string.Equals(element.GetString(), expected, StringComparison.Ordinal);

    private static bool TryReadUtc(JsonElement element, out DateTimeOffset value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(element.GetString(), out value)
            && value != default
            && value.Offset == TimeSpan.Zero;
    }

    private static bool IsNullOrString(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null or JsonValueKind.String;

    private static TrustedLegalEntityScopeProviderResult Fail(int statusCode, string failureCode) =>
        TrustedLegalEntityScopeProviderResult.Fail(statusCode, failureCode);

    private readonly record struct ParsedEnvelope(
        int StatusCode,
        bool IsSuccessful,
        IReadOnlyList<string> Errors,
        JsonElement? Data);
}
