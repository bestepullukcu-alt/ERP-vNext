namespace Diten.AuthService.Domain.Entities;

public abstract class EntityBase : GlobalEntityBase
{
    /// <summary>
    /// Multi-tenant ayırt edici.
    /// </summary>
    public Guid TenantId { get; init; }
}
