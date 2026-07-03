using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>FEAT-AUDIT-RBAC — see <see cref="IRbacAuditRecorder"/>. Reuses the existing <see cref="IAuthAuditService"/>
/// (authAuditLogs), stamping the actor from <see cref="ICurrentUserAccessor"/>. Fail-safe: never throws.</summary>
public sealed class RbacAuditRecorder : IRbacAuditRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IAuthAuditService _auditService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogger<RbacAuditRecorder> _logger;

    public RbacAuditRecorder(
        IAuthAuditService auditService,
        ICurrentUserAccessor currentUser,
        ILogger<RbacAuditRecorder> logger)
    {
        _auditService = auditService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default)
    {
        try
        {
            var actorId = _currentUser.UserId;
            var node = JsonSerializer.SerializeToNode(metadata, JsonOptions) as JsonObject ?? new JsonObject();
            node["actorId"] = actorId?.ToString();
            await _auditService.WriteAsync(eventName, actorId, tenantId, node.ToJsonString(), ct);
        }
        catch (Exception ex)
        {
            // Audit must NOT break the RBAC mutation (which already succeeded) — but never swallow silently: log loudly.
            _logger.LogError(ex, "Failed to write RBAC audit event {EventName} for tenant {TenantId}.", eventName, tenantId);
        }
    }
}
