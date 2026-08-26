namespace Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;

public abstract class EntityBase
{
    protected EntityBase(Guid id, Guid tenantId, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Identifiers must be non-empty.");
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("CreatedAtUtc must be UTC.");
        Id = id; TenantId = tenantId; CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public Guid TenantId { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime? UpdatedAtUtc { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAtUtc { get; protected set; }
    public int Version { get; protected set; }

    protected void Touch(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.");
        UpdatedAtUtc = nowUtc;
        checked { Version++; }
    }
}
