using System.Net.Http.Json;
using System.Security.Claims;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// BL-456 — forwards AuthService audit events to Platform's central store via the internal S2S endpoint
/// (<c>POST /api/internal/audit/append</c>, X-Internal-Api-Key). A copy of MDM's <c>PlatformAuditForwarder</c>, on the
/// typed-client shape the other Platform clients here use (<see cref="PlatformTenantEntitlementClient"/>): same
/// <see cref="PlatformServiceOptions"/> base URL + key. Actor, e-mail and correlation id come from the current request;
/// Platform masks the e-mail with its own rule. Best-effort: every failure is logged and swallowed.
/// <para>Synchronous, not the outbox: AuthService's <c>OutboxMessage</c> has no dispatcher (measured 2026-09-25 — the
/// type is referenced by nothing), so it would queue events nobody sends.</para>
/// </summary>
public sealed class PlatformAuditForwarder : IPlatformAuditForwarder
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string SourceServiceName = "Diten.AuthService";
    private const string SourceModule = "access-governance";

    // Platform AuditCategory.IdentityAccess — an existing category; this bridge opens no new one.
    private const int CategoryIdentityAccess = 2;

    // Mirrors Diten.Platform.Domain.Enums.AuditActorType.
    private const int ActorPlatformAdministrator = 1;
    private const int ActorPartnerAdministrator = 2;
    private const int ActorTenantUser = 3;
    private const int ActorSystem = 4;

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PlatformServiceOptions _options;
    private readonly ILogger<PlatformAuditForwarder> _logger;

    public PlatformAuditForwarder(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformAuditForwarder> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ForwardAsync(PlatformAuditEvent auditEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning("Audit forwarding skipped: Platform internal API key is not configured. RequestType={RequestType}", auditEvent.RequestType);
            return;
        }

        // Tenant boundary: a tenant-scoped event without its tenant is never sent (Platform would refuse it anyway).
        if (auditEvent.TenantId == Guid.Empty)
        {
            _logger.LogWarning("Audit forwarding skipped: no tenant. RequestType={RequestType}", auditEvent.RequestType);
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var (actorType, actorId) = ResolveActor(user);

        string? correlationHeader = httpContext?.Request.Headers[CorrelationIdHeader];
        var correlationId = Guid.TryParse(correlationHeader, out var incoming) && incoming != Guid.Empty
            ? incoming
            : Guid.NewGuid();

        var payload = new
        {
            CorrelationId = correlationId,
            RequestType = auditEvent.RequestType,
            ActorType = actorType,
            ActorId = actorId,
            ActorEmail = user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst("email")?.Value,
            // Owner, 2026-09-25: without it Platform names the INTERNAL caller ("system" → "s***m") as the actor on every
            // forwarded row. The token carries the person's given/family name (TokenService); Platform masks it.
            ActorDisplayName = ActorDisplayName(user),
            TargetTenantId = auditEvent.TenantId,
            Category = CategoryIdentityAccess,
            EntityType = auditEvent.EntityType,
            EntityId = auditEvent.EntityId,
            Operation = auditEvent.Operation,
            Outcome = auditEvent.Outcome,
            Metadata = auditEvent.Metadata,
            BeforeState = auditEvent.BeforeState,
            AfterState = auditEvent.AfterState,
            SourceService = SourceServiceName,
            SourceModule,
            IsPlatformGlobal = false,
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/audit/append")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId.ToString());

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Audit forwarding got {StatusCode} from Platform. RequestType={RequestType} TenantId={TenantId}",
                    (int)response.StatusCode,
                    auditEvent.RequestType,
                    auditEvent.TenantId);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller went away (request aborted, shutting down) while the event was on its way. The operation it
            // describes already happened and its local authAuditLogs row is written — but Platform's log may now lack
            // the row, and that must not pass in silence.
            _logger.LogWarning(
                "Audit forwarding was cancelled before Platform answered. RequestType={RequestType} TenantId={TenantId}",
                auditEvent.RequestType,
                auditEvent.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Audit forwarding could not reach Platform. RequestType={RequestType} TenantId={TenantId}",
                auditEvent.RequestType,
                auditEvent.TenantId);
        }
    }

    private static (int ActorType, Guid? ActorId) ResolveActor(ClaimsPrincipal? user)
    {
        var actorId = ReadGuid(user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value);
        var actorType = user?.FindFirst("actor_type")?.Value switch
        {
            "tenant_user" => ActorTenantUser,
            "platform_admin" => ActorPlatformAdministrator,
            "partner_admin" => ActorPartnerAdministrator,
            _ => actorId.HasValue ? ActorTenantUser : ActorSystem
        };

        return (actorType, actorId);
    }

    private static string? ActorDisplayName(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var given = user.FindFirst(ClaimTypes.GivenName)?.Value ?? user.FindFirst("given_name")?.Value;
        var family = user.FindFirst(ClaimTypes.Surname)?.Value ?? user.FindFirst("family_name")?.Value;
        var full = string.Join(' ', new[] { given, family }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (!string.IsNullOrWhiteSpace(full)) return full;
        var name = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("name")?.Value;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static Guid? ReadGuid(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed : null;
}
