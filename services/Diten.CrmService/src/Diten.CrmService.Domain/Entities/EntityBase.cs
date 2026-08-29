namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// Tenant-owned CRM entity base. TenantId is server-resolved (never from client payload).
/// Soft-delete via IsDeleted/DeletedAt. Version is the technical concurrency token (NOT a business field).
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public int Version { get; set; }
}
