namespace Diten.SupplyChainService.Domain.Common;
public abstract class EntityBase
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int Version { get; set; }
}
