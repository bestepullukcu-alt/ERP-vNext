using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// BL-459 — <see cref="IUserQuotaClient"/> over Platform's internal quota endpoints, the typed-client shape of
/// <see cref="PlatformTenantEntitlementClient"/>. The same contract Platform's own tenant-admin invite uses in process
/// (InviteTenantAdminUserCommandHandler asks <c>users.max</c> before inviting; F1: a count, nothing to release).
/// <para>Only Platform's explicit 409 <c>QUOTA_LIMIT_EXCEEDED</c> refuses. Everything else — unreachable, timeout, no
/// usage row, configuration missing, a malformed answer — is <see cref="UserQuotaOutcome.Unavailable"/> and logs the
/// <c>quota_unavailable</c> warning: the owner chose "open" over "locked out" (BL-459).</para>
/// </summary>
public sealed class PlatformUserQuotaClient : IUserQuotaClient
{
    public const string QuotaKey = "users.max";
    public const string LimitExceededCode = "QUOTA_LIMIT_EXCEEDED";
    public const string UnavailableLogEvent = "quota_unavailable";

    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string Source = "Diten.AuthService";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly PlatformServiceOptions _options;
    private readonly ILogger<PlatformUserQuotaClient> _logger;

    public PlatformUserQuotaClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserAccessor currentUser,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformUserQuotaClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UserQuotaDecision> TryConsumeUserSeatAsync(Guid tenantId, string operationReference, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            // Never a quota call without its tenant; nothing to enforce against.
            return Unavailable(tenantId, "no tenant");
        }

        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return Unavailable(tenantId, "Platform internal API key is not configured");
        }

        try
        {
            using var response = await SendAsync("/api/internal/quotas/consume", tenantId, operationReference, "Tenant user create.", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var envelope = JsonSerializer.Deserialize<ConsumeEnvelope>(body, JsonOptions);
                return envelope is { IsSuccessful: true, Data.Applied: true }
                    ? new UserQuotaDecision(UserQuotaOutcome.Consumed)
                    : Unavailable(tenantId, $"consume answered {(int)response.StatusCode} without an applied seat");
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var refusal = JsonSerializer.Deserialize<ConsumeEnvelope>(body, JsonOptions);
                if (refusal?.Errors?.Contains(LimitExceededCode, StringComparer.Ordinal) == true)
                {
                    return new UserQuotaDecision(UserQuotaOutcome.LimitExceeded, refusal.Quota?.LimitValue, refusal.Quota?.CurrentValue);
                }
            }

            return Unavailable(tenantId, $"consume answered {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // "Never throws" is the contract: any failure to READ the quota is "unavailable" (the create proceeds and says so);
            // only the caller's own cancellation passes through.
            _logger.LogWarning(ex, "{Event}: user quota could not be read; the create proceeds. TenantId={TenantId}", UnavailableLogEvent, tenantId);
            return new UserQuotaDecision(UserQuotaOutcome.Unavailable);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, Guid tenantId, string reference, string reason, CancellationToken ct)
    {
        var correlationId = ResolveCorrelationId();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            // Platform's TryConsumeQuotaRequest.
            Content = JsonContent.Create(new
            {
                TenantId = tenantId,
                QuotaKey,
                Amount = 1m,
                Source,
                OperationId = Guid.NewGuid().ToString(),
                SourceReference = reference,
                Reason = reason,
                ActorId = _currentUser.UserId?.ToString(),
                CorrelationId = correlationId
            })
        };
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId);
        return await _httpClient.SendAsync(request, ct);
    }

    private UserQuotaDecision Unavailable(Guid tenantId, string why)
    {
        _logger.LogWarning("{Event}: user quota could not be read ({Reason}); the create proceeds. TenantId={TenantId}", UnavailableLogEvent, why, tenantId);
        return new UserQuotaDecision(UserQuotaOutcome.Unavailable);
    }

    private string ResolveCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;
        var existing = context?.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(existing) ? existing : context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    }

    // Platform's Response<QuotaMutationDto> — and, on the limit refusal, the added quota snapshot (BL-459).
    private sealed record ConsumeEnvelope(bool IsSuccessful, MutationRow? Data, List<string>? Errors, QuotaRow? Quota);

    private sealed record MutationRow(bool Applied);

    private sealed record QuotaRow(decimal LimitValue, decimal CurrentValue);
}
