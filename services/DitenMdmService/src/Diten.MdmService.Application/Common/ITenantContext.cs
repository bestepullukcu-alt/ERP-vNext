namespace Diten.MdmService.Application.Common;

/// <summary>
/// Scoped tenant bilgisi — TenantResolutionMiddleware tarafından populate edilir.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsResolved { get; }
}
