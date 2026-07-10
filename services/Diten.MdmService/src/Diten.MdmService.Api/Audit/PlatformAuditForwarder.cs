using System.Net.Http.Json;
using System.Security.Claims;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Application.Contracts.Audit;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Audit;

/// <summary>
/// Forwards MDM audit events to Platform's central store via the internal S2S endpoint
/// (<c>POST /api/internal/audit/append</c>), reusing the SAME Platform base URL + X-Internal-Api-Key already configured
/// for module self-registration (<see cref="PlatformRegistrationOptions"/>). Actor + tenant are read from the current
/// request's JWT. Best-effort: every failure is logged and swallowed.
/// </summary>
public sealed class PlatformAuditForwarder : IPlatformAuditForwarder
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string SourceServiceName = "Diten.MDM";

    // Mirrors Diten.Platform.Domain.Enums.AuditActorType.
    private const int ActorPlatformAdministrator = 1;
    private const int ActorPartnerAdministrator = 2;
    private const int ActorTenantUser = 3;
    private const int ActorSystem = 4;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PlatformRegistrationOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PlatformAuditForwarder> _logger;

    public PlatformAuditForwarder(
        IHttpClientFactory httpClientFactory,
        IOptions<PlatformRegistrationOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PlatformAuditForwarder> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task ForwardAsync(AuditForwardRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogDebug("Audit forwarding skipped: PlatformRegistration BaseUrl/InternalApiKey not configured.");
            return;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var (actorType, actorId) = ResolveActor(user);
        var tenantId = ReadGuid(user?.FindFirst("tenant_id")?.Value);

        var payload = new
        {
            CorrelationId = Guid.NewGuid(),
            RequestType = request.RequestType,
            ActorType = actorType,
            ActorId = actorId,
            ActorEmail = user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst("email")?.Value,
            TargetTenantId = tenantId,
            Category = request.Category,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Operation = request.Operation,
            Outcome = request.Outcome,
            SourceService = SourceServiceName,
            SourceModule = request.SourceModule,
            IsPlatformGlobal = false,
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}/api/internal/audit/append")
            {
                Content = JsonContent.Create(payload)
            };
            httpRequest.Headers.Add(InternalApiKeyHeader, _options.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Audit forwarding got {StatusCode} from Platform. RequestType={RequestType} EntityType={EntityType}",
                    (int)response.StatusCode,
                    request.RequestType,
                    request.EntityType);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Request aborted / shutting down — nothing to do.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Audit forwarding could not reach Platform. RequestType={RequestType} EntityType={EntityType}",
                request.RequestType,
                request.EntityType);
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

    private static Guid? ReadGuid(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed : null;
}
