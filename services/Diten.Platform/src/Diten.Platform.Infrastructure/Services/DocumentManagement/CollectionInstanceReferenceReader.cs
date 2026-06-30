using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Infrastructure.Services.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — read-only adapter over the mixed read/write <see cref="ICollectionInstanceRepository"/>. It
/// wraps the repository internally but exposes ONLY read-only operations; no create/archive/reactivate/provision
/// member of the repository is reachable through this seam. FU01 never mutates the CollectionInstance.
/// </summary>
public sealed class CollectionInstanceReferenceReader : ICollectionInstanceReferenceReader
{
    private readonly ICollectionInstanceRepository _repository;

    public CollectionInstanceReferenceReader(ICollectionInstanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<CollectionInstanceReferenceDto?> ResolveByIdAsync(Guid collectionInstanceId, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(collectionInstanceId, ct);
        return instance is null ? null : Map(instance);
    }

    public async Task<bool> ValidateScopeAsync(Guid collectionInstanceId, Guid companyId, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(collectionInstanceId, ct);
        return instance is not null && instance.CompanyId == companyId;
    }

    public async Task<CollectionPathSnapshot?> GetPathSnapshotAsync(Guid collectionInstanceId, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(collectionInstanceId, ct);
        return instance is null
            ? null
            : new CollectionPathSnapshot(instance.Id, instance.CompanyId, instance.CanonicalId, instance.FullPath);
    }

    public async Task<CollectionInstanceCompanyBinding?> GetCompanyBindingAsync(Guid collectionInstanceId, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(collectionInstanceId, ct);
        return instance is null
            ? null
            : new CollectionInstanceCompanyBinding(instance.CompanyId, MapBindings(instance));
    }

    public async Task<bool> IsUsableAsync(Guid collectionInstanceId, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(collectionInstanceId, ct);
        return instance is { InstanceStatus: CollectionInstanceStatus.Active };
    }

    public async Task<IReadOnlyList<CollectionInstanceReferenceDto>> GetBranchAsync(Guid rootCollectionInstanceId, CancellationToken ct = default)
    {
        var root = await _repository.GetByIdAsync(rootCollectionInstanceId, ct);
        if (root is null)
        {
            return [];
        }

        var siblings = await _repository.GetByCompanyAsync(root.CompanyId, ct);
        var prefix = root.FullPath + "/";
        return siblings
            .Where(x => x.Id == root.Id || x.FullPath == root.FullPath || x.FullPath.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(x => x.FullPath, StringComparer.Ordinal)
            .Select(Map)
            .ToList();
    }

    public async Task<IReadOnlyList<CollectionInstanceReferenceDto>> GetCompanyInstancesAsync(Guid companyId, CancellationToken ct = default)
    {
        var instances = await _repository.GetByCompanyAsync(companyId, ct);
        return instances
            .OrderBy(x => x.FullPath, StringComparer.Ordinal)
            .Select(Map)
            .ToList();
    }

    private static CollectionInstanceReferenceDto Map(CollectionInstance instance) => new(
        instance.Id,
        instance.CompanyId,
        instance.BaselineReleaseId,
        instance.CanonicalId,
        instance.ParentCanonicalId,
        instance.Name,
        instance.FullPath,
        instance.InstanceStatus.ToString().ToUpperInvariant(),
        instance.InstanceStatus == CollectionInstanceStatus.Active,
        MapBindings(instance),
        instance.DisplayOrder);

    private static IReadOnlyList<CollectionInstanceScopeBindingDto> MapBindings(CollectionInstance instance) =>
        instance.ScopeBindings
            .Select(b => new CollectionInstanceScopeBindingDto(
                b.OrgBindingScopeType.ToString().ToUpperInvariant(),
                b.OrgBindingScopeId,
                b.BindingStatus.ToString().ToUpperInvariant()))
            .ToList();
}
