using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IBusinessReferenceDataStewardshipRepository
{
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
