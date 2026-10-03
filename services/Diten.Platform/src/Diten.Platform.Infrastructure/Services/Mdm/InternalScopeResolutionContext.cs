using Diten.Platform.Application.Common;

namespace Diten.Platform.Infrastructure.Services.Mdm;

public sealed class InternalScopeResolutionContext : IInternalScopeResolutionContext
{
    private Guid _tenantId;
    private Guid _actorId;

    public bool IsBound { get; private set; }
    public Guid TenantId => IsBound ? _tenantId : throw new InvalidOperationException("Internal scope context is not bound.");
    public Guid ActorId => IsBound ? _actorId : throw new InvalidOperationException("Internal scope context is not bound.");

    public void Bind(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Tenant and actor identifiers must be non-empty.");
        }

        if (IsBound && (_tenantId != tenantId || _actorId != actorId))
        {
            throw new InvalidOperationException("Internal scope context cannot be rebound to another tenant or actor.");
        }

        _tenantId = tenantId;
        _actorId = actorId;
        IsBound = true;
    }
}
