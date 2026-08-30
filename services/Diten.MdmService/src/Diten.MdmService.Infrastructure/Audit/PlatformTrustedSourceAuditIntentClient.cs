using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.Audit;

public sealed class PlatformTrustedSourceAuditIntentClient : ITrustedSourceAuditIntentClient
{
    private const string AcceptPath = "/api/internal/v1/audit/source-intents/accept";
    private const int MaximumResponseBytes = 32 * 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TrustedSourceAuditIntentClientOptions _options;
    private readonly TimeProvider _clock;

    public PlatformTrustedSourceAuditIntentClient(
        IHttpClientFactory httpClientFactory,
        IOptions<TrustedSourceAuditIntentClientOptions> options,
        TimeProvider clock)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<TrustedSourceAuditIntentDeliveryResult> AcceptAsync(
        TrustedSourceAuditIntentEnvelope envelope,
        TrustedSourceAuditServiceIdentity identity,
        CancellationToken cancellationToken = default)
    {
        EnsureValidConfiguration(_options);
        ValidateEnvelope(envelope, _clock.GetUtcNow());
        if (string.IsNullOrWhiteSpace(identity.AccessToken) || identity.AccessToken.Length > 8192)
            return TrustedSourceAuditIntentDeliveryResult.AuthenticationRejected("AUDIT_SOURCE_INTENT_UNAUTHENTICATED");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.PlatformBaseUrl), AcceptPath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.AccessToken);
            request.Content = new ByteArrayContent(SerializeEnvelope(envelope));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            using var response = await _httpClientFactory.CreateClient(nameof(PlatformTrustedSourceAuditIntentClient))
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);

            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                    return TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_RESPONSE_INVALID");
                var bytes = await ReadBoundedAsync(response.Content, budget.Token);
                try
                {
                    var receipt = ParseReceipt(bytes, envelope, response.StatusCode == HttpStatusCode.OK);
                    return TrustedSourceAuditIntentDeliveryResult.Accepted(receipt);
                }
                catch (InvalidOperationException)
                {
                    return TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_RESPONSE_INVALID");
                }
            }

            return Classify(response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TrustedSourceAuditIntentDeliveryResult.Retryable("AUDIT_SOURCE_INTENT_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return TrustedSourceAuditIntentDeliveryResult.Retryable("AUDIT_SOURCE_INTENT_UNAVAILABLE");
        }
    }

    private TrustedSourceAuditIntentAcceptanceReceipt ParseReceipt(
        byte[] utf8,
        TrustedSourceAuditIntentEnvelope envelope,
        bool expectedDuplicate)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8, StrictDocumentOptions);
            var root = document.RootElement;
            RequireExactProperties(root, "data", "statusCode", "isSuccessful", "errors", "reason_code", "correlation_id");
            if (!root.GetProperty("isSuccessful").GetBoolean()
                || root.GetProperty("statusCode").GetInt32() != (expectedDuplicate ? 200 : 201)
                || root.GetProperty("errors").GetArrayLength() != 0
                || root.GetProperty("reason_code").ValueKind != JsonValueKind.Null
                || root.GetProperty("correlation_id").ValueKind != JsonValueKind.String
                || !IsBounded(root.GetProperty("correlation_id").GetString(), 256))
                throw new InvalidOperationException();

            var data = root.GetProperty("data");
            RequireExactProperties(data, "centralAcknowledgement", "centralIdempotencyKey", "contractVersion", "acceptedAt", "duplicate");
            var acknowledgement = ReadBoundedString(data, "centralAcknowledgement", 512);
            var centralKey = ReadBoundedString(data, "centralIdempotencyKey", 512);
            var contract = ReadBoundedString(data, "contractVersion", 128);
            var acceptedAt = data.GetProperty("acceptedAt").GetDateTimeOffset();
            var duplicate = data.GetProperty("duplicate").GetBoolean();
            var expectedKey = $"{AuditIntentContract.SourceService}:{envelope.TenantId:N}:{envelope.IntentId:N}:{AuditIntentDeliveryProcessor.RequiredContractVersion}";
            if (duplicate != expectedDuplicate || acceptedAt.Offset != TimeSpan.Zero
                || acceptedAt > _clock.GetUtcNow().AddMinutes(5)
                || !string.Equals(contract, AuditIntentDeliveryProcessor.RequiredContractVersion, StringComparison.Ordinal)
                || !string.Equals(centralKey, expectedKey, StringComparison.Ordinal))
                throw new InvalidOperationException();

            return new(acknowledgement, centralKey, contract, acceptedAt, duplicate);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidOperationException("AUDIT_SOURCE_INTENT_RESPONSE_INVALID", exception);
        }
    }

    private static TrustedSourceAuditIntentDeliveryResult Classify(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            TrustedSourceAuditIntentDeliveryResult.AuthenticationRejected("AUDIT_SOURCE_INTENT_AUTHENTICATION_REJECTED"),
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable =>
            TrustedSourceAuditIntentDeliveryResult.Retryable("AUDIT_SOURCE_INTENT_UNAVAILABLE"),
        HttpStatusCode.GatewayTimeout => TrustedSourceAuditIntentDeliveryResult.Retryable("AUDIT_SOURCE_INTENT_TIMEOUT"),
        _ when (int)status >= 500 => TrustedSourceAuditIntentDeliveryResult.Retryable("AUDIT_SOURCE_INTENT_UNAVAILABLE"),
        HttpStatusCode.BadRequest => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_INVALID"),
        HttpStatusCode.Conflict => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT"),
        _ => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_REJECTED")
    };

    public static void EnsureValidConfiguration(TrustedSourceAuditIntentClientOptions options)
    {
        if (!Uri.TryCreate(options.PlatformBaseUrl, UriKind.Absolute, out var uri)
            || !IsSecureInternalUri(uri)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new InvalidOperationException("AUDIT_SOURCE_INTENT_CLIENT_CONFIGURATION_INVALID");
    }

    private static bool IsSecureInternalUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;

    private static void ValidateEnvelope(TrustedSourceAuditIntentEnvelope value, DateTimeOffset now)
    {
        if (!string.Equals(value.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal)
            || !string.Equals(value.ContractVersion, AuditIntentDeliveryProcessor.RequiredContractVersion, StringComparison.Ordinal)
            || value.IntentId == Guid.Empty || value.TenantId == Guid.Empty || value.AggregateId == Guid.Empty
            || value.CorrelationId == Guid.Empty || value.TimestampUtc.Offset != TimeSpan.Zero
            || value.TimestampUtc > now.AddMinutes(5)
            || value.PreVersion < -1 || value.PostVersion != value.PreVersion + 1 || value.Sequence < 0
            || !IsBounded(value.AggregateType, 160) || !IsBounded(value.Operation, 160)
            || !IsBounded(value.ActorId, 160) || !IsBounded(value.CausationId, 160)
            || !IsBounded(value.CommandId, 160) || !IsBounded(value.IdempotencyKey, 240)
            || value.SnapshotReference is not null
               && !Regex.IsMatch(value.SnapshotReference, "^[A-Za-z0-9._:/-]{1,256}$", RegexOptions.CultureInvariant)
            || value.EvidenceHash.Length != 64 || value.EvidenceHash.Any(ch => ch is < '0' or > '9' and < 'A' or > 'F'))
            throw new InvalidOperationException("AUDIT_SOURCE_INTENT_ENVELOPE_INVALID");
    }

    private static byte[] SerializeEnvelope(TrustedSourceAuditIntentEnvelope value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("sourceService", value.SourceService);
            writer.WriteString("contractVersion", value.ContractVersion);
            writer.WriteString("intentId", value.IntentId);
            writer.WriteString("tenantId", value.TenantId);
            writer.WriteString("aggregateType", value.AggregateType);
            writer.WriteString("aggregateId", value.AggregateId);
            writer.WriteNumber("preVersion", value.PreVersion);
            writer.WriteNumber("postVersion", value.PostVersion);
            writer.WriteString("operation", value.Operation);
            writer.WriteString("actorId", value.ActorId);
            writer.WriteString("correlationId", value.CorrelationId);
            writer.WriteString("causationId", value.CausationId);
            writer.WriteString("commandId", value.CommandId);
            writer.WriteNumber("sequence", value.Sequence);
            writer.WriteString("timestampUtc", value.TimestampUtc);
            writer.WriteString("evidenceHash", value.EvidenceHash);
            if (value.SnapshotReference is null) writer.WriteNull("snapshotReference");
            else writer.WriteString("snapshotReference", value.SnapshotReference);
            writer.WriteString("idempotencyKey", value.IdempotencyKey);
            writer.WriteEndObject();
        }
        if (stream.Length > 32 * 1024) throw new InvalidOperationException("AUDIT_SOURCE_INTENT_TOO_LARGE");
        return stream.ToArray();
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[2048];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) return destination.ToArray();
            if (destination.Length + read > MaximumResponseBytes)
                throw new InvalidOperationException("AUDIT_SOURCE_INTENT_RESPONSE_INVALID");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static string ReadBoundedString(JsonElement element, string name, int maximum)
    {
        var property = element.GetProperty(name);
        var value = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        return IsBounded(value, maximum) ? value! : throw new InvalidOperationException();
    }

    private static bool IsBounded(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);

    private static void RequireExactProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidOperationException();
        var found = element.EnumerateObject().Select(item => item.Name).ToArray();
        if (found.Length != names.Length || found.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(name => !found.Contains(name, StringComparer.Ordinal)))
            throw new InvalidOperationException();
    }

    private static readonly JsonDocumentOptions StrictDocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8
    };
}
