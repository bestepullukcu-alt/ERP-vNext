namespace Diten.Platform.Common.Persistence;

public abstract class GlobalEntity : BaseEntity
{
    // No TenantId here
}

public abstract class TenantScopedEntity : BaseEntity
{
    public required Guid TenantId { get; init; }
}

public abstract class HybridEntity : BaseEntity
{
    public Guid? TenantId { get; init; }

    public bool IsGlobal => TenantId == null;
}
