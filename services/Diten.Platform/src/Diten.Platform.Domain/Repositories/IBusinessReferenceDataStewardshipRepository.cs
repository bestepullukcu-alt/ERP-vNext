using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IBusinessReferenceDataStewardshipRepository
{
    Guid GetRequiredReferenceTenantId();
    Task<(IReadOnlyList<BusinessReferenceDataSet> Items, long TotalCount)> QuerySetsAsync(BusinessReferenceDataSetListQuery query, CancellationToken ct = default);
    Task<bool> IsCatalogGovernedSetAsync(Guid setId, CancellationToken ct = default);
    Task<bool> IsCatalogGovernedVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<BusinessReferenceDataSet?> GetSetByIdAsync(Guid setId, CancellationToken ct = default);
    Task<BusinessReferenceDataSet?> GetSetByCodeAsync(string setCode, CancellationToken ct = default);
    Task CreateSetAsync(BusinessReferenceDataSet entity, CancellationToken ct = default);
    Task<bool> UpdateSetAsync(BusinessReferenceDataSet entity, long expectedRowVersion, CancellationToken ct = default);

    Task<BusinessReferenceDataVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessReferenceDataVersion>> GetVersionsByIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessReferenceDataVersion>> GetVersionsBySetIdAsync(Guid setId, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessReferenceDataVersion>> GetPublishedVersionsBySetAsync(Guid setId, Guid excludingVersionId, CancellationToken ct = default);
    Task<int> GetNextVersionNumberAsync(Guid setId, CancellationToken ct = default);
    Task<bool> HasActiveDraftVersionAsync(Guid setId, CancellationToken ct = default);
    Task CreateVersionAsync(BusinessReferenceDataVersion version, CancellationToken ct = default);
    Task<bool> UpdateVersionAsync(BusinessReferenceDataVersion version, string expectedConcurrencyToken, CancellationToken ct = default);
    Task<int> DeprecatePublishedVersionsAsync(Guid setId, Guid keepVersionId, Guid supersededByVersionId, CancellationToken ct = default);
    Task ReplaceValidationResultsAsync(Guid versionId, IReadOnlyList<BusinessReferenceDataValidationResult> results, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessReferenceDataValidationResult>> GetValidationResultsByVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<bool> IntegrationEventExistsAsync(Guid versionId, string eventName, string idempotencyKey, CancellationToken ct = default);
    Task SaveIntegrationEventAsync(BusinessReferenceDataIntegrationEvent integrationEvent, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessReferenceDataVersion>> GetPublishedVersionsBySetCodeAsync(string setCode, CancellationToken ct = default);
    Task<BusinessReferenceDataUsageRegistration> UpsertUsageRegistrationAsync(BusinessReferenceDataUsageRegistration registration, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessReferenceDataUsageRegistration>> GetUsageRegistrationsAsync(string setCode, CancellationToken ct = default);
    Task<BusinessReferenceDataUsageRegistration?> GetUsageRegistrationByIdAsync(Guid usageRegistrationId, CancellationToken ct = default);
    Task<bool> DeactivateUsageRegistrationAsync(Guid usageRegistrationId, string actorId, CancellationToken ct = default);
    Task<BusinessReferenceDataUsageImpactSummary> GetUsageImpactSummaryAsync(string setCode, CancellationToken ct = default);
    Task<bool> UpdateSetUsageSummaryAsync(string setCode, int totalRegistrations, int criticalRegistrations, DateTimeOffset updatedAt, CancellationToken ct = default);
    Task CreateTenantAssignmentAsync(BusinessReferenceDataTenantAssignment assignment, CancellationToken ct = default);
    Task<BusinessReferenceDataTenantAssignment?> GetTenantAssignmentByIdAsync(Guid assignmentId, Guid consumerTenantId, CancellationToken ct = default);
    Task<BusinessReferenceDataTenantAssignment?> GetActiveTenantAssignmentAsync(Guid consumerTenantId, string setCode, CancellationToken ct = default);
    Task<BusinessReferenceDataTenantAssignment?> GetTenantAssignmentForReconciliationAsync(Guid consumerTenantId, string setCode, CancellationToken ct = default);
    Task<BusinessReferenceDataTenantAssignmentReconciliationResult> EnsureActiveTenantAssignmentAsync(
        Guid consumerTenantId,
        string setCode,
        string actorId,
        CancellationToken ct = default);
    Task<bool> RevokeTenantAssignmentAsync(Guid assignmentId, Guid consumerTenantId, int expectedVersion, string actorId, CancellationToken ct = default);
    Task<bool> ReactivateTenantAssignmentAsync(Guid assignmentId, Guid consumerTenantId, int expectedVersion, string actorId, CancellationToken ct = default);
    Task<bool> SoftDeleteTenantAssignmentAsync(Guid assignmentId, Guid consumerTenantId, int expectedVersion, string actorId, CancellationToken ct = default);
    Task<BusinessReferenceDataPublishOperationCreateResult> CreateOrGetPublishOperationAsync(BusinessReferenceDataPublishOperation operation, CancellationToken ct = default);
    Task<BusinessReferenceDataPublishOperation?> GetPublishOperationByIdAsync(Guid publishOperationId, CancellationToken ct = default);
    Task<BusinessReferenceDataPublishOperation?> GetPublishOperationByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<bool> IsPublishOperationVerifiedAsync(Guid publishOperationId, CancellationToken ct = default);
    Task<BusinessReferenceDataVerifiedPublication?> GetVerifiedPublicationAsync(
        string setCode,
        CancellationToken ct = default);
    Task<BusinessReferenceDataVerifiedPublication?> GetVerifiedPublicationAsync(
        string setCode,
        string catalogVersion,
        string catalogFingerprint,
        CancellationToken ct = default);
    Task<bool> TransitionPublishOperationAsync(
        Guid publishOperationId,
        int expectedVersion,
        BusinessReferenceDataPublishOperationState nextState,
        BusinessReferenceDataPublishCheckpoint nextCheckpoint,
        string actorId,
        string? errorCode = null,
        CancellationToken ct = default);
    Task CreateImportPreviewAsync(BusinessReferenceDataImportPreview preview, CancellationToken ct = default);
    Task<BusinessReferenceDataImportPreview?> GetImportPreviewByIdAsync(Guid previewId, CancellationToken ct = default);
    Task<bool> UpdateImportPreviewAsync(BusinessReferenceDataImportPreview preview, CancellationToken ct = default);
}

public sealed record BusinessReferenceDataSetListQuery(
    string? Search,
    string? Status,
    string? ScopeType,
    int Page,
    int PageSize,
    string Sort,
    bool CatalogGovernedOnly = false);

public sealed record BusinessReferenceDataUsageImpactSummary(
    string SetCode,
    int TotalRegistrations,
    int CriticalRegistrations,
    int HighRegistrations,
    int MediumRegistrations,
    int LowRegistrations,
    DateTimeOffset? LastRegisteredAt);

public enum BusinessReferenceDataPublishOperationCreateOutcome
{
    Created,
    Replayed,
    Conflict
}

public sealed record BusinessReferenceDataPublishOperationCreateResult(
    BusinessReferenceDataPublishOperationCreateOutcome Outcome,
    BusinessReferenceDataPublishOperation Operation);

public enum BusinessReferenceDataTenantAssignmentReconciliationOutcome
{
    Created,
    Replayed,
    Conflict
}

public sealed record BusinessReferenceDataTenantAssignmentReconciliationResult(
    BusinessReferenceDataTenantAssignmentReconciliationOutcome Outcome,
    BusinessReferenceDataTenantAssignment Assignment);

public sealed record BusinessReferenceDataVerifiedPublication(
    BusinessReferenceDataSet Set,
    BusinessReferenceDataVersion Version,
    BusinessReferenceDataPublishOperation Operation);
