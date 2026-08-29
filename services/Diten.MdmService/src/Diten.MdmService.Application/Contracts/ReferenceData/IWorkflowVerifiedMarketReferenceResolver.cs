namespace Diten.MdmService.Application.Contracts.ReferenceData;

/// <summary>
/// Resolves current verified Market membership for background workflow recovery.
/// This contract never delegates an interactive user token.
/// </summary>
public interface IWorkflowVerifiedMarketReferenceResolver
{
    Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(
        Guid tenantId,
        string marketCode,
        CancellationToken cancellationToken = default);
}
