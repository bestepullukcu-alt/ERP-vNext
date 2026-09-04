using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Microsoft.Extensions.Configuration;

namespace Diten.PpmService.Infrastructure.Entitlements;

public sealed class PpmEntitlementDecisionClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ICorrelationContext correlationContext) : IPpmEntitlementDecisionClient
{
    public const string ContractName = "platform.ppm-entitlement-decision.v1";
    public const string ModuleCode = "PPM";
    public const string ServiceCredentialHeader = "X-PPM-Service-Key";
    public const string CorrelationIdHeader = "X-Correlation-Id";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<bool> IsAllowedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            throw new PpmEntitlementDependencyException("A valid server tenant context is required.");
        }

        var enabled = configuration.GetValue<bool>("PpmEntitlementDecision:Enabled");
        var baseUrl = configuration["PpmEntitlementDecision:BaseUrl"];
        var credential = configuration["PpmEntitlementDecision:ServiceCredential"];
        if (!enabled || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
                     || string.IsNullOrWhiteSpace(credential))
        {
            throw new PpmEntitlementDependencyException(
                "The authoritative PPM entitlement decision provider is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(baseUri, $"/api/internal/ppm/tenants/{tenantId:D}/entitlement-decision"));
        request.Headers.TryAddWithoutValidation(ServiceCredentialHeader, credential);
        request.Headers.TryAddWithoutValidation(
            CorrelationIdHeader,
            correlationContext.CorrelationId.ToString("D"));

        var timeoutSeconds = configuration.GetValue<int?>("PpmEntitlementDecision:TimeoutSeconds") ?? 5;
        if (timeoutSeconds is < 1 or > 30)
        {
            throw new PpmEntitlementDependencyException("The PPM entitlement dependency timeout is invalid.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new PpmEntitlementDependencyException(
                    $"The authoritative PPM entitlement provider returned {(int)response.StatusCode}.");
            }

            var payload = await ReadBoundedPayloadAsync(response.Content, timeout.Token);
            if (payload.Length == 0)
            {
                throw new PpmEntitlementDependencyException(
                    "The authoritative PPM entitlement response size is invalid.");
            }

            using var document = JsonDocument.Parse(payload);
            var properties = document.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
            var expectedProperties = new[]
            {
                "tenantId", "moduleCode", "isAllowed", "resolvedAtUtc", "expiresAtUtc"
            };
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || properties.Length != expectedProperties.Length
                || !properties.OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(expectedProperties.OrderBy(x => x, StringComparer.Ordinal),
                        StringComparer.Ordinal))
            {
                throw new PpmEntitlementDependencyException(
                    "The authoritative PPM entitlement response shape is invalid.");
            }

            var decision = JsonSerializer.Deserialize<PpmEntitlementDecisionResponse>(payload, JsonOptions);
            if (decision is null
                || decision.TenantId != tenantId
                || !string.Equals(decision.ModuleCode, ModuleCode, StringComparison.Ordinal)
                || decision.ResolvedAtUtc == default
                || decision.ResolvedAtUtc.Offset != TimeSpan.Zero
                || (decision.ExpiresAtUtc.HasValue
                    && decision.ExpiresAtUtc.Value.Offset != TimeSpan.Zero)
                || (decision.IsAllowed
                    && decision.ExpiresAtUtc.HasValue
                    && decision.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow))
            {
                throw new PpmEntitlementDependencyException(
                    "The authoritative PPM entitlement response is invalid.");
            }

            return decision.IsAllowed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PpmEntitlementDependencyException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or OperationCanceledException
                or JsonException
                or NotSupportedException)
        {
            throw new PpmEntitlementDependencyException(
                "The authoritative PPM entitlement decision is unavailable.",
                exception);
        }
    }

    private static async Task<byte[]> ReadBoundedPayloadAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        const int maximumBytes = 4096;
        var buffer = new byte[maximumBytes + 1];
        var totalRead = 0;
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);

        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        if (totalRead > maximumBytes)
        {
            throw new PpmEntitlementDependencyException(
                "The authoritative PPM entitlement response size is invalid.");
        }

        return buffer.AsSpan(0, totalRead).ToArray();
    }

    private sealed record PpmEntitlementDecisionResponse(
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("moduleCode")] string ModuleCode,
        [property: JsonPropertyName("isAllowed")] bool IsAllowed,
        [property: JsonPropertyName("resolvedAtUtc")] DateTimeOffset ResolvedAtUtc,
        [property: JsonPropertyName("expiresAtUtc")] DateTimeOffset? ExpiresAtUtc);
}
