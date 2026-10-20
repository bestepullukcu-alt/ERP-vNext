using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Diten.BuildingBlocks.Auditing;

/// <summary>Who called, in which tenant, under which correlation id — read once per record by the forwarder.</summary>
public interface IAuditCallContext
{
    AuditCaller Resolve();
}

/// <summary>
/// The caller of one audited command. <see cref="ActorEmail"/> / <see cref="ActorDisplayName"/> are the name AT THAT
/// TIME (AUD-001 §3 #2); Platform masks them before storing.
/// </summary>
public sealed record AuditCaller(
    Guid? TenantId,
    AuditActorType ActorType,
    Guid? ActorId,
    string? ActorEmail,
    string? ActorDisplayName,
    Guid? CorrelationId);

/// <summary>
/// The default caller: the request's token (<c>sub</c> / name identifier, <c>actor_type</c>, <c>tenant_id</c>, e-mail,
/// name) and <c>X-Correlation-Id</c>. A service whose tenant is resolved server-side (not from the token) wraps this and
/// overrides <see cref="AuditCaller.TenantId"/>.
/// </summary>
public sealed class HttpAuditCallContext : IAuditCallContext
{
    public const string CorrelationHeader = "X-Correlation-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAuditCallContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuditCaller Resolve()
    {
        var context = _httpContextAccessor.HttpContext;
        var user = context?.User;
        var actorId = ReadGuid(user?.FindFirst("sub")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var actorType = user?.FindFirst("actor_type")?.Value switch
        {
            "tenant_user" => AuditActorType.TenantUser,
            "platform_admin" => AuditActorType.PlatformAdministrator,
            "partner_admin" => AuditActorType.PartnerAdministrator,
            _ => actorId.HasValue ? AuditActorType.TenantUser : AuditActorType.System
        };
        var isUser = actorType != AuditActorType.System;

        return new AuditCaller(
            TenantId: ReadGuid(user?.FindFirst("tenant_id")?.Value),
            ActorType: actorType,
            ActorId: actorId,
            ActorEmail: isUser ? user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst("email")?.Value : null,
            ActorDisplayName: isUser ? user?.FindFirst("name")?.Value ?? user?.FindFirst(ClaimTypes.Name)?.Value : null,
            CorrelationId: ReadGuid(context?.Request.Headers[CorrelationHeader].FirstOrDefault()));
    }

    private static Guid? ReadGuid(string? value) =>
        Guid.TryParse(value?.Trim(), out var parsed) && parsed != Guid.Empty ? parsed : null;
}
