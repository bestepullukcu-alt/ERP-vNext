namespace Diten.WebAPI.Security;

public sealed class EsbpTenantContext
{
    public Guid TenantId { get; private set; }

    public string ActorId { get; private set; } = string.Empty;

    public bool IsResolved { get; private set; }

    internal void Resolve(Guid tenantId, string actorId)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException("The ES tenant context is immutable once resolved.");
        }

        TenantId = tenantId;
        ActorId = actorId;
        IsResolved = true;
    }
}
