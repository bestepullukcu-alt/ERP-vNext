namespace Diten.Platform.Application.Common;

/// <summary>
/// Request-scoped identity bound only by an authenticated internal scope-resolution endpoint.
/// It is not populated from tenant headers and does not select a Legal Entity.
/// </summary>
public interface IInternalScopeResolutionContext
{
    bool IsBound { get; }
    Guid TenantId { get; }
    Guid ActorId { get; }

    void Bind(Guid tenantId, Guid actorId);
}
