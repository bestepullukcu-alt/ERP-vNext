using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.RecordCodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.RecordCodes;

/// <summary>
/// The ONE adapter of <see cref="IRecordCodeAllocator"/> (RECORD-CODE-ALLOCATE 1.0.0, FROZEN): <c>POST
/// /api/internal/record-codes/allocate</c> on Platform, service to service, on behalf of the user — the service credential
/// (binding <c>Mdm</c>) in the three <c>X-Record-Code-*</c> headers and the user's own bearer token forwarded; no
/// <c>X-Tenant-Id</c> (the tenant is the token's). It reads the answer as the contract's envelope and nothing else:
/// <list type="bullet">
/// <item>200 with <c>isSuccessful = true</c> and <c>data.code</c> (+ <c>data.sequence</c>) → allocated; a 200 that is not that
/// (<c>isSuccessful</c> not true, empty <c>data</c>, no code) is NOT an allocation;</item>
/// <item>403 <c>RECORD_CODE_PERMISSION_MISSING</c> / <c>LEGAL_ENTITY_READ_PERMISSION_MISSING</c> → permission missing (Create 403);</item>
/// <item>404 <c>LEGAL_ENTITY_NOT_FOUND</c> → legal entity unknown (Create 422);</item>
/// <item>everything else — other statuses (a ProblemDetails <c>AUDIT_*</c> included), a body that is not the envelope, a
/// timeout, an unreachable host, missing configuration, no user token — → unavailable (Create 503).</item>
/// </list>
/// It never retries, never makes up a code and never falls back to anything.
/// </summary>
public sealed class PlatformRecordCodeAllocatorClient : IRecordCodeAllocator
{
    public const string CredentialIdHeader = "X-Record-Code-Credential-Id";
    public const string CredentialSecretHeader = "X-Record-Code-Credential";
    public const string AudienceHeader = "X-Record-Code-Audience";
    public const string Audience = "TRUSTED_RECORD_CODE_ALLOCATE";
    public const string RelativePath = "api/internal/record-codes/allocate";

    private const int MaximumResponseBytes = 16_384;

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RecordCodeAllocatorOptions _options;

    public PlatformRecordCodeAllocatorClient(
        HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IOptions<RecordCodeAllocatorOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<RecordCodeAllocationResult> AllocateAsync(
        RecordCodePrefixScope prefixScope,
        Guid? legalEntityId,
        string recordType,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The contract's scope rule (§2): LegalEntity needs a company, Group / Platform carry none. A call that breaks it
        // never leaves the process (the service would answer 422 RECORD_CODE_SCOPE_INVALID anyway).
        var scopeFits = prefixScope == RecordCodePrefixScope.LegalEntity
            ? legalEntityId is { } company && company != Guid.Empty
            : legalEntityId is null;
        if (string.IsNullOrWhiteSpace(recordType) || string.IsNullOrWhiteSpace(idempotencyKey) || !scopeFits)
            return RecordCodeAllocationResult.Unavailable("RECORD_CODE_REQUEST_INVALID");

        if (!IsConfigured()) return RecordCodeAllocationResult.Unavailable("RECORD_CODE_PORT_NOT_BOUND");
        if (!TryGetDelegatedBearer(out var bearer)) return RecordCodeAllocationResult.Unavailable("RECORD_CODE_UNAUTHENTICATED");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.PlatformBaseAddress!, RelativePath));
        request.Headers.Authorization = bearer;
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, Audience);
        request.Content = new StringContent(CreateBody(prefixScope, legalEntityId, recordType, idempotencyKey), Encoding.UTF8, "application/json");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            var payload = IsJson(response.Content.Headers.ContentType)
                ? await ReadBoundedAsync(response.Content, budget.Token)
                : null;
            return Map(response.StatusCode, payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RecordCodeAllocationResult.Unavailable("RECORD_CODE_TIMEOUT");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return RecordCodeAllocationResult.Unavailable("RECORD_CODE_UNREACHABLE");
        }
    }

    private bool IsConfigured() =>
        _options.PlatformBaseAddress is { IsAbsoluteUri: true } baseAddress
        && (baseAddress.Scheme == Uri.UriSchemeHttps || baseAddress.Scheme == Uri.UriSchemeHttp && baseAddress.IsLoopback)
        && _options.Timeout > TimeSpan.Zero
        && _options.Timeout <= TimeSpan.FromSeconds(30)
        && !string.IsNullOrWhiteSpace(_options.CredentialIdentifier)
        && !string.IsNullOrEmpty(_options.CredentialSecret);

    private bool TryGetDelegatedBearer(out AuthenticationHeaderValue? bearer)
    {
        bearer = null;
        var context = _httpContextAccessor.HttpContext;
        var values = context?.Request.Headers.Authorization;
        if (context?.User.Identity?.IsAuthenticated != true || values is null || values.Value.Count != 1) return false;
        if (!AuthenticationHeaderValue.TryParse(values.Value[0], out var parsed)
            || !string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(parsed.Parameter))
            return false;
        bearer = parsed;
        return true;
    }

    private static string CreateBody(RecordCodePrefixScope prefixScope, Guid? legalEntityId, string recordType, string idempotencyKey)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("recordType", recordType);
            writer.WriteString("prefixScope", prefixScope.ToString());
            if (legalEntityId is { } entity) writer.WriteString("legalEntityId", entity.ToString("D"));
            else writer.WriteNull("legalEntityId");
            writer.WriteString("idempotencyKey", idempotencyKey);
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
        if (content.Headers.ContentLength is > MaximumResponseBytes) return null;
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumResponseBytes + 1];
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(bytesRead, buffer.Length - bytesRead), cancellationToken);
            if (read == 0) break;
            bytesRead += read;
        }

        return bytesRead is > 0 and <= MaximumResponseBytes ? buffer[..bytesRead] : null;
    }

    /// <summary>The contract's answer table (§3, §4) in one place; public so the mapping is measured without a network.</summary>
    public static RecordCodeAllocationResult Map(HttpStatusCode status, byte[]? payload)
    {
        if (payload is null) return RecordCodeAllocationResult.Unavailable("RECORD_CODE_CONTRACT_INVALID");
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return RecordCodeAllocationResult.Unavailable("RECORD_CODE_CONTRACT_INVALID");

            if (status == HttpStatusCode.OK)
            {
                // 200 is an allocation only when the envelope says so: isSuccessful true AND a data object holding a code.
                if (!root.TryGetProperty("isSuccessful", out var ok) || ok.ValueKind != JsonValueKind.True
                    || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                    || !data.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(code.GetString())
                    || !data.TryGetProperty("sequence", out var sequence) || sequence.ValueKind != JsonValueKind.Number
                    || !sequence.TryGetInt64(out var number) || number < 1)
                    return RecordCodeAllocationResult.Unavailable("RECORD_CODE_CONTRACT_INVALID");
                return RecordCodeAllocationResult.Allocated(code.GetString()!, number);
            }

            var reason = root.TryGetProperty("reason_code", out var reasonElement) && reasonElement.ValueKind == JsonValueKind.String
                ? reasonElement.GetString()
                : null;
            return status switch
            {
                HttpStatusCode.Forbidden when reason is "RECORD_CODE_PERMISSION_MISSING" or "LEGAL_ENTITY_READ_PERMISSION_MISSING"
                    => RecordCodeAllocationResult.PermissionMissing(reason),
                HttpStatusCode.NotFound when reason == "LEGAL_ENTITY_NOT_FOUND" => RecordCodeAllocationResult.LegalEntityUnknown(),
                _ => RecordCodeAllocationResult.Unavailable(reason)
            };
        }
        catch (JsonException)
        {
            return RecordCodeAllocationResult.Unavailable("RECORD_CODE_CONTRACT_INVALID");
        }
    }
}
