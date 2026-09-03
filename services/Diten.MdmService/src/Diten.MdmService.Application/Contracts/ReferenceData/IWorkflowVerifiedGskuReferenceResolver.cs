namespace Diten.MdmService.Application.Contracts.ReferenceData;

/// <summary>
/// Resolves current verified GSKU reference evidence for background workflow processing.
/// This contract uses the tenant-bound trusted reference-data service identity and never an interactive bearer.
/// </summary>
public interface IWorkflowVerifiedGskuReferenceResolver
{
    Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
        Guid tenantId,
        string packApplicabilityValueCode,
        string uomValueCode,
        CancellationToken cancellationToken = default);
}
