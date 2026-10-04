using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class PlatformAdministratorStatusClient : IPlatformAdministratorStatusClient
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    private readonly PlatformServiceOptions _options;
    private readonly ILogger<PlatformAdministratorStatusClient> _logger;

    public PlatformAdministratorStatusClient(
        HttpClient httpClient,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformAdministratorStatusClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsActiveAsync(string email, CancellationToken ct) =>
        (await ReadStatusAsync(email, ct)).Status?.IsActive == true;

    public async Task<PlatformAdministratorRecordAnswer> GetActiveActorTypeAsync(string email, CancellationToken ct)
    {
        var (failure, status) = await ReadStatusAsync(email, ct);
        if (failure is not null)
        {
            return failure;
        }

        // A record that is not active (suspended, disabled, deleted) is Platform's definitive no.
        if (status!.IsActive != true)
        {
            return PlatformAdministratorRecordAnswer.Refused;
        }

        // BL-521 FIX1 F1(b) — the record's enum name (PlatformAdmin / PartnerAdmin) as the token actor type. A value that
        // IS sent and is unknown is no type a token can carry: refused (FIX2 item 3, a definitive no).
        // FIX3 item 5 — NO value at all is an older Platform that never sent the field: a version gap while a deployment
        // rolls, not a statement about this administrator. No answer (503, nothing revoked) — as a definitive no it ended
        // every platform session that refreshed through an old copy in a mixed fleet.
        if (status.ActorType is null)
        {
            _logger.LogWarning("Platform administrator status answered without an actor type (an older Platform?). Email={Email}", email.Trim().ToLowerInvariant());
            return PlatformAdministratorRecordAnswer.Unavailable;
        }

        return status.ActorType switch
        {
            { } value when string.Equals(value, "PartnerAdmin", StringComparison.OrdinalIgnoreCase)
                => PlatformAdministratorRecordAnswer.ConfirmedAs(PlatformAdministratorRecordAnswer.PartnerAdminActorType),
            { } value when string.Equals(value, "PlatformAdmin", StringComparison.OrdinalIgnoreCase)
                => PlatformAdministratorRecordAnswer.ConfirmedAs(PlatformAdministratorRecordAnswer.PlatformAdminActorType),
            _ => PlatformAdministratorRecordAnswer.Refused
        };
    }

    /// <summary>
    /// BL-521 FIX2 item 3 — Platform's status reply, or why there is none. A failure is either Platform's definitive no
    /// (Refused: its own 404 envelope, or no address to ask about) or NO ANSWER (Unavailable: network error, timeout, 5xx,
    /// any other non-record status, an unreadable body, this service missing its internal key). The two used to be one
    /// "null": every non-2xx reply — a 503 during a deployment included — ended the administrator's sessions, while a
    /// network error or a bad body escaped as a 500 and ended nothing.
    /// </summary>
    private async Task<(PlatformAdministratorRecordAnswer? Failure, PlatformAdministratorStatusSnapshot? Status)> ReadStatusAsync(
        string email,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (PlatformAdministratorRecordAnswer.Refused, null);
        }

        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogError("Platform internal API key is not configured.");
            return (PlatformAdministratorRecordAnswer.Unavailable, null);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/internal/platform-administrators/status?email={Uri.EscapeDataString(normalizedEmail)}");
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Platform's own "no such administrator" carries its envelope (404, not successful). A 404 without it — a
                // route that does not exist, a wrong base address — is not an answer about the record.
                var notFound = await response.Content.ReadFromJsonAsync<PlatformEnvelope<PlatformAdministratorStatusSnapshot>>(JsonOptions, ct);
                if (notFound is { IsSuccessful: false, StatusCode: 404 })
                {
                    return (PlatformAdministratorRecordAnswer.Refused, null);
                }

                _logger.LogWarning("Platform administrator status answered 404 without its envelope. Email={Email}", normalizedEmail);
                return (PlatformAdministratorRecordAnswer.Unavailable, null);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // BL-521 FIX3 item 7 — Platform refused THIS service's internal key (wrong, or missing on one side): a
                // configuration fault, not a passing outage. Every platform sign-in, refresh and forced change answers 503
                // until it is fixed, so it is an error. The status only — never the key or its value.
                _logger.LogError(
                    "Platform refused this service's internal API key on the administrator status call. StatusCode={StatusCode} Every platform sign-in answers 503 until the key is configured the same on both services.",
                    (int)response.StatusCode);
                return (PlatformAdministratorRecordAnswer.Unavailable, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Platform administrator status read failed. Email={Email} StatusCode={StatusCode}",
                    normalizedEmail,
                    (int)response.StatusCode);
                return (PlatformAdministratorRecordAnswer.Unavailable, null);
            }

            var envelope = await response.Content.ReadFromJsonAsync<PlatformEnvelope<PlatformAdministratorStatusSnapshot>>(JsonOptions, ct);
            if (envelope?.Data is null)
            {
                _logger.LogWarning("Platform administrator status answered without a record. Email={Email}", normalizedEmail);
                return (PlatformAdministratorRecordAnswer.Unavailable, null);
            }

            return (null, envelope.Data);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // the caller gave up; not Platform's answer
        }
        catch (Exception ex)
        {
            // Network error, HttpClient timeout, an unreadable body: no answer — never a "no".
            _logger.LogWarning(ex, "Platform administrator status could not be read. Email={Email}", normalizedEmail);
            return (PlatformAdministratorRecordAnswer.Unavailable, null);
        }
    }

    public async Task MarkLoginAcceptedAsync(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/platform-administrators/accept-login")
        {
            Content = JsonContent.Create(new { email = normalizedEmail })
        };
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Platform administrator login acceptance update failed. Email={Email} StatusCode={StatusCode}",
                    normalizedEmail,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Platform administrator login acceptance update failed. Email={Email}",
                normalizedEmail);
        }
    }

    private string ResolveCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;
        var existing = context?.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        return context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    }

    private sealed record PlatformEnvelope<T>(bool IsSuccessful, int StatusCode, T? Data);
    private sealed record PlatformAdministratorStatusSnapshot(Guid Id, string Email, string Status, bool IsDeleted, bool IsActive, string? ActorType = null);
}
