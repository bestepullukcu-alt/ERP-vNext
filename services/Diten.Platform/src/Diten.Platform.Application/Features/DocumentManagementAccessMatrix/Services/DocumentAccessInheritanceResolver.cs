using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;

/// <summary>One node in a target's access ancestry; distance 0 is the target itself, larger is farther (more general).</summary>
public sealed record AccessAncestor(DocumentAccessTargetType TargetType, string TargetId, int Distance);

/// <summary>
/// MOD-0029-FU04 — builds the access inheritance chain for a target:
/// <c>company -> documentation structure -> folder -> document/template -> variant</c> (nearest first). It is a
/// read-only traversal over existing FU01/FU02/FU03 read seams and MOD-0028 read-only folder reader; it never
/// mutates any structure. The structure (documentation) level is keyed by the folder's BaselineReleaseId as the
/// CollectionDefinition-level ancestor identifier.
/// </summary>
public sealed class DocumentAccessInheritanceResolver
{
    private readonly ITemplateVariantRepository _variants;
    private readonly ITemplateDocumentRepository _templateDocuments;
    private readonly IControlledDocumentRepository _controlledDocuments;
    private readonly ITemplateMasterRepository _masters;
    private readonly ICollectionInstanceReferenceReader _folders;
    private readonly ITenantContext _tenantContext;

    // Per-request (Scoped) memoization of read-only folder reference data. Resolving a whole collection-instances
    // list otherwise re-runs the full company-instances scan once PER row (O(N²)); every row in that list shares the
    // same company, so one snapshot serves them all. Reference data does not change within a single request.
    private readonly Dictionary<Guid, IReadOnlyList<CollectionInstanceReferenceDto>> _companyInstancesCache = new();
    private readonly Dictionary<Guid, CollectionInstanceReferenceDto?> _folderByIdCache = new();

    public DocumentAccessInheritanceResolver(
        ITemplateVariantRepository variants,
        ITemplateDocumentRepository templateDocuments,
        IControlledDocumentRepository controlledDocuments,
        ITemplateMasterRepository masters,
        ICollectionInstanceReferenceReader folders,
        ITenantContext tenantContext)
    {
        _variants = variants;
        _templateDocuments = templateDocuments;
        _controlledDocuments = controlledDocuments;
        _masters = masters;
        _folders = folders;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<AccessAncestor>> BuildAncestorsAsync(
        DocumentAccessTargetType targetType,
        string targetId,
        CancellationToken ct)
    {
        var chain = new List<(DocumentAccessTargetType Type, string Id)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(DocumentAccessTargetType type, string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var key = $"{type}:{id.Trim()}".ToLowerInvariant();
            if (seen.Add(key)) chain.Add((type, id.Trim()));
        }

        Add(targetType, targetId);

        // Walk specific → general. Bounded by the fixed aggregate depth; no cycles by construction (seen set).
        switch (targetType)
        {
            case DocumentAccessTargetType.TemplateVariant when Guid.TryParse(targetId, out var variantId):
            {
                var variant = await _variants.GetByIdAsync(variantId, ct);
                if (variant?.LinkedTemplateDocumentId is { } linked && linked != Guid.Empty)
                {
                    await AppendTemplateDocumentChainAsync(linked, Add, ct);
                }
                else if (variant?.OwnerCompanyId is { } company && company != Guid.Empty)
                {
                    Add(DocumentAccessTargetType.Company, company.ToString("D"));
                }
                break;
            }
            case DocumentAccessTargetType.TemplateDocument when Guid.TryParse(targetId, out var templateId):
                await AppendTemplateDocumentChainAsync(templateId, Add, ct, includeSelf: false);
                break;
            case DocumentAccessTargetType.ControlledDocument when Guid.TryParse(targetId, out var docId):
            {
                var doc = await _controlledDocuments.GetByIdAsync(docId, ct);
                if (doc is not null)
                {
                    await AppendFolderChainAsync(doc.CollectionInstanceId, Add, ct);
                }
                break;
            }
            case DocumentAccessTargetType.CollectionInstance when Guid.TryParse(targetId, out var folderId):
                await AppendFolderChainAsync(folderId, Add, ct, includeSelf: false);
                break;
            case DocumentAccessTargetType.TemplateMaster when Guid.TryParse(targetId, out var masterId):
            {
                var master = await _masters.GetByIdAsync(masterId, ct);
                if (master?.OwnerCompanyId is { } company && company != Guid.Empty)
                {
                    Add(DocumentAccessTargetType.Company, company.ToString("D"));
                }
                AddTenant(Add);
                break;
            }
            case DocumentAccessTargetType.CollectionDefinition:
            case DocumentAccessTargetType.Company:
                AddTenant(Add);
                break;
            case DocumentAccessTargetType.Tenant:
            default:
                break;
        }

        return chain.Select((c, i) => new AccessAncestor(c.Type, c.Id, i)).ToList();
    }

    private async Task AppendTemplateDocumentChainAsync(Guid templateId, Action<DocumentAccessTargetType, string?> add, CancellationToken ct, bool includeSelf = true)
    {
        if (includeSelf) add(DocumentAccessTargetType.TemplateDocument, templateId.ToString("D"));
        var template = await _templateDocuments.GetByIdAsync(templateId, ct);
        if (template?.CollectionInstanceId is { } folderId && folderId != Guid.Empty)
        {
            await AppendFolderChainAsync(folderId, add, ct);
        }
        else if (template is not null)
        {
            add(DocumentAccessTargetType.Company, template.OwnerCompanyId.ToString("D"));
            AddTenant(add);
        }
    }

    private async Task AppendFolderChainAsync(Guid folderId, Action<DocumentAccessTargetType, string?> add, CancellationToken ct, bool includeSelf = true)
    {
        if (includeSelf) add(DocumentAccessTargetType.CollectionInstance, folderId.ToString("D"));

        var folder = await ResolveFolderCachedAsync(folderId, ct);
        if (folder is null)
        {
            AddTenant(add);
            return;
        }

        // Parent folders via the ParentCanonicalId chain within the same company's instances.
        var instances = await GetCompanyInstancesCachedAsync(folder.CompanyId, ct);
        var byCanonical = instances
            .GroupBy(i => i.CanonicalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var current = folder;
        var guard = 0;
        while (current is not null && !string.IsNullOrWhiteSpace(current.ParentCanonicalId) && guard++ < 64)
        {
            if (!byCanonical.TryGetValue(current.ParentCanonicalId!, out var parent)) break;
            add(DocumentAccessTargetType.CollectionInstance, parent.CollectionInstanceId.ToString("D"));
            current = parent;
        }

        // Documentation structure level (CollectionDefinition), keyed by the baseline release id.
        add(DocumentAccessTargetType.CollectionDefinition, folder.BaselineReleaseId.ToString("D"));
        add(DocumentAccessTargetType.Company, folder.CompanyId.ToString("D"));
        AddTenant(add);
    }

    private async Task<CollectionInstanceReferenceDto?> ResolveFolderCachedAsync(Guid folderId, CancellationToken ct)
    {
        if (_folderByIdCache.TryGetValue(folderId, out var cached))
        {
            return cached;
        }

        var folder = await _folders.ResolveByIdAsync(folderId, ct);
        _folderByIdCache[folderId] = folder;
        return folder;
    }

    private async Task<IReadOnlyList<CollectionInstanceReferenceDto>> GetCompanyInstancesCachedAsync(Guid companyId, CancellationToken ct)
    {
        if (_companyInstancesCache.TryGetValue(companyId, out var cached))
        {
            return cached;
        }

        var instances = await _folders.GetCompanyInstancesAsync(companyId, ct);
        _companyInstancesCache[companyId] = instances;
        // Seed the by-id cache so sibling rows in the same company resolve their own folder without another read.
        foreach (var instance in instances)
        {
            _folderByIdCache[instance.CollectionInstanceId] = instance;
        }

        return instances;
    }

    private void AddTenant(Action<DocumentAccessTargetType, string?> add)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId != Guid.Empty)
        {
            add(DocumentAccessTargetType.Tenant, tenantId.ToString("D"));
        }
    }
}
