using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataConsumerQueryService
{
    Task<BusinessReferenceDataPublishedValuesModel> GetPublishedValuesAsync(
        string setCode,
        string? scopeKey,
        CancellationToken ct = default);

    Task<BusinessReferenceDataValuesLookupModel> GetValuesAsync(
        string setCode,
        string? scopeKey,
        int? versionNumber,
        DateTimeOffset? asOfDate,
        bool includeDeprecated,
        bool includeAttributes,
        bool includeMappings,
        CancellationToken ct = default);

    Task<BusinessReferenceDataHierarchyLookupModel> GetHierarchyAsync(
        string setCode,
        string? scopeKey,
        int? versionNumber,
        DateTimeOffset? asOfDate,
        bool includeDeprecated,
        bool includeAttributes,
        bool includeMappings,
        CancellationToken ct = default);

    Task<BusinessReferenceDataUsageRegistrationResultModel> RegisterUsageAsync(
        string setCode,
        string consumerModule,
        string consumerName,
        string? consumerEndpoint,
        string? scopeType,
        string? scopeKey,
        int? versionPin,
        DateTimeOffset? asOfDate,
        string? resolutionMode,
        string? criticality,
        string? notes,
        string actorId,
        string correlationId,
        CancellationToken ct = default);

    Task<BusinessReferenceDataUsageRegistrationListModel> GetUsageRegistrationsAsync(
        string setCode,
        CancellationToken ct = default);

    Task<bool> DeactivateUsageRegistrationAsync(
        Guid usageRegistrationId,
        string actorId,
        string correlationId,
        CancellationToken ct = default);

    Task<int> DeactivateUsageRegistrationsBulkAsync(
        IReadOnlyCollection<Guid> usageRegistrationIds,
        string actorId,
        string correlationId,
        CancellationToken ct = default);
}
