using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU01 — controlled-document / template / version / share repository contracts. Every method is
// tenant-scoped via the TenantRepository ExecutionFilter; no hard delete.

public interface IControlledDocumentRepository
{
    Task<ControlledDocument> CreateAsync(ControlledDocument document, CancellationToken ct = default);
    Task<ControlledDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ControlledDocument?> GetByDocumentKeyAsync(string documentKey, CancellationToken ct = default);
    Task<IReadOnlyList<ControlledDocument>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ControlledDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyList<ControlledDocument>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default);
    Task<bool> UpdateAsync(ControlledDocument document, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IDocumentFavoriteRepository
{
    Task ToggleAsync(Guid userId, Guid documentId, bool favorite, CancellationToken ct = default);
    Task<bool> IsFavoriteAsync(Guid userId, Guid documentId, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetFavoriteDocumentIdsAsync(Guid userId, CancellationToken ct = default);
}

public interface IControlledDocumentVersionRepository
{
    Task<ControlledDocumentVersion> CreateAsync(ControlledDocumentVersion version, CancellationToken ct = default);
    Task<ControlledDocumentVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ControlledDocumentVersion>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<ControlledDocumentVersion?> GetByDocumentAndNumberAsync(Guid documentId, int versionNumber, CancellationToken ct = default);
    Task<int> GetMaxVersionNumberAsync(Guid documentId, CancellationToken ct = default);
    Task SupersedeActiveVersionsAsync(Guid documentId, Guid exceptVersionId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITemplateDocumentRepository
{
    Task<TemplateDocument> CreateAsync(TemplateDocument template, CancellationToken ct = default);
    Task<TemplateDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TemplateDocument?> GetByTemplateKeyAsync(string templateKey, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateDocument>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TemplateDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateDocument>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default);
    Task<bool> UpdateAsync(TemplateDocument template, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITemplateVersionRepository
{
    Task<TemplateVersion> CreateAsync(TemplateVersion version, CancellationToken ct = default);
    Task<TemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVersion>> GetByTemplateAsync(Guid templateId, CancellationToken ct = default);
    Task<TemplateVersion?> GetByTemplateAndNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default);
    Task<int> GetMaxVersionNumberAsync(Guid templateId, CancellationToken ct = default);
    Task SupersedeActiveVersionsAsync(Guid templateId, Guid exceptVersionId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITemplateMasterRepository
{
    Task<TemplateMaster> CreateAsync(TemplateMaster master, CancellationToken ct = default);
    Task<TemplateMaster?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TemplateMaster?> GetByMasterCodeAsync(string masterCode, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateMaster>> ListAsync(
        string? status,
        string? classification,
        Guid? collectionDefinitionId,
        string? canonicalId,
        string? variantPolicy,
        CancellationToken ct = default);
    Task<bool> UpdateAsync(TemplateMaster master, CancellationToken ct = default);
    Task<int> CountByCurrentVersionAsync(Guid templateMasterVersionId, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}

public interface ITemplateMasterVersionRepository
{
    Task<TemplateMasterVersion> CreateAsync(TemplateMasterVersion version, CancellationToken ct = default);
    Task<TemplateMasterVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateMasterVersion>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default);
    Task<TemplateMasterVersion?> GetByMasterAndNumberAsync(Guid templateMasterId, int versionNumber, CancellationToken ct = default);
    Task<int> GetMaxVersionNumberAsync(Guid templateMasterId, CancellationToken ct = default);
    Task SupersedePublishedVersionsAsync(Guid templateMasterId, Guid exceptVersionId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITemplateVariantRepository
{
    Task<TemplateVariant> CreateAsync(TemplateVariant variant, CancellationToken ct = default);
    Task<TemplateVariant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TemplateVariant?> GetByScopeAndCodeAsync(
        Enums.DocumentManagement.TemplateVariantScopeType scopeType,
        Guid scopeId,
        string variantCode,
        CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVariant>> ListAsync(
        Guid? templateMasterId,
        string? scopeType,
        Guid? scopeId,
        string? status,
        string? approvalStatus,
        CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVariant>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default);
    Task<bool> UpdateAsync(TemplateVariant variant, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IFolderDocumentAccessPolicyRepository
{
    Task<FolderDocumentAccessPolicy> UpsertAsync(FolderDocumentAccessPolicy policy, CancellationToken ct = default);
    Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default);
    Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAndTargetsAsync(
        Guid collectionInstanceId,
        IReadOnlyList<(Enums.DocumentManagement.AccessTargetType TargetType, string TargetId)> targets,
        CancellationToken ct = default);
}

public interface IDocumentShareRecordRepository
{
    Task<DocumentShareRecord> CreateAsync(DocumentShareRecord share, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentShareRecord>> CreateManyAsync(IReadOnlyList<DocumentShareRecord> shares, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentShareRecord>> GetByItemAsync(Enums.DocumentManagement.SharedItemKind itemKind, Guid itemId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentShareRecord>> GetSharesForTargetCompanyAsync(Guid targetCompanyId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Enums.DocumentManagement.SharedItemKind itemKind, Guid itemId, Guid targetCompanyId, CancellationToken ct = default);
}

public interface IFolderShareOperationRepository
{
    Task<FolderShareOperation> CreateAsync(FolderShareOperation operation, CancellationToken ct = default);
    Task<FolderShareOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default);
}

public interface IFolderShareOutcomeRepository
{
    Task<IReadOnlyList<FolderShareOutcome>> CreateManyAsync(IReadOnlyList<FolderShareOutcome> outcomes, CancellationToken ct = default);
    Task<IReadOnlyList<FolderShareOutcome>> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default);
}
