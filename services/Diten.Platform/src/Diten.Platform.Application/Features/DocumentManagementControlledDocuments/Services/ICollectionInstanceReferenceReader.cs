namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — dedicated READ-ONLY seam over the MOD-0028-FU05 <c>CollectionInstance</c>. FU01
/// handlers/services/controllers consume folder metadata ONLY through this reader and must NEVER inject the
/// mixed read/write <c>ICollectionInstanceRepository</c> (which exposes Create/Archive/Reactivate). The
/// Infrastructure adapter may wrap that repository internally but exposes read-only methods only; no
/// create/archive/reactivate/provision operation is reachable from FU01 through this seam. FU01 never mutates
/// the CollectionInstance, CollectionDefinition, or BaselineRelease.
/// </summary>
public interface ICollectionInstanceReferenceReader
{
    /// <summary>Tenant-scoped resolve; null means the folder does not exist / is not visible (caller maps to 404
    /// non-leakage).</summary>
    Task<CollectionInstanceReferenceDto?> ResolveByIdAsync(Guid collectionInstanceId, CancellationToken ct = default);

    /// <summary>Company/legal-entity scope check for the target folder.</summary>
    Task<bool> ValidateScopeAsync(Guid collectionInstanceId, Guid companyId, CancellationToken ct = default);

    /// <summary>FullPath + CanonicalId + CompanyId snapshot to copy into document metadata at attach time.</summary>
    Task<CollectionPathSnapshot?> GetPathSnapshotAsync(Guid collectionInstanceId, CancellationToken ct = default);

    /// <summary>CompanyId + scope bindings (legal entity / plant / business unit).</summary>
    Task<CollectionInstanceCompanyBinding?> GetCompanyBindingAsync(Guid collectionInstanceId, CancellationToken ct = default);

    /// <summary>InstanceStatus == Active / usable for attach/upload.</summary>
    Task<bool> IsUsableAsync(Guid collectionInstanceId, CancellationToken ct = default);

    /// <summary>Read-only descendants of a root node, derived from the FullPath prefix / ParentCanonicalId chain.
    /// The root itself is included as the first element.</summary>
    Task<IReadOnlyList<CollectionInstanceReferenceDto>> GetBranchAsync(Guid rootCollectionInstanceId, CancellationToken ct = default);
}

/// <summary>Read DTO. TenantId stays internal-only (resolved from tenant context, never returned to the client).</summary>
public sealed record CollectionInstanceReferenceDto(
    Guid CollectionInstanceId,
    Guid CompanyId,
    Guid BaselineReleaseId,
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string FullPath,
    string InstanceStatus,
    bool IsUsable,
    IReadOnlyList<CollectionInstanceScopeBindingDto> ScopeBindings);

public sealed record CollectionInstanceScopeBindingDto(
    string ScopeType,
    Guid ScopeId,
    string BindingStatus);

public sealed record CollectionPathSnapshot(
    Guid CollectionInstanceId,
    Guid CompanyId,
    string CanonicalId,
    string FullPath);

public sealed record CollectionInstanceCompanyBinding(
    Guid CompanyId,
    IReadOnlyList<CollectionInstanceScopeBindingDto> ScopeBindings);
