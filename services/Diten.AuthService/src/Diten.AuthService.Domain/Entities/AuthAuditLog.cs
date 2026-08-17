namespace Diten.AuthService.Domain.Entities;

public sealed class AuthAuditLog : GlobalEntityBase
{
    private AuthAuditLog() { }

    public AuthAuditLog(string eventName, Guid userId, Guid tenantId, string metadata)
    {
        EventName = eventName;
        UserId = userId;
        TenantId = tenantId;
        Metadata = metadata;
        OccurredAt = DateTimeOffset.UtcNow;
        CreatedBy = "auth-service";
    }

    public string EventName { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Metadata { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
}
