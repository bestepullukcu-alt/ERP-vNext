using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class BusinessReferenceDataConsumerQueryService : IBusinessReferenceDataConsumerQueryService
{
    private static readonly IReadOnlyList<string> AllowedScopeTypes = BusinessReferenceDataScopeTypes.Codes;
    private static readonly string[] AllowedResolutionModes = ["latest", "pinned", "as-of"];
    private static readonly string[] AllowedCriticalities = ["low", "medium", "high"];

    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public BusinessReferenceDataConsumerQueryService(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<BusinessReferenceDataPublishedValuesModel> GetPublishedValuesAsync(
        string setCode,
        string? scopeKey,
        CancellationToken ct = default)
    {
        var (set, version, effectiveAt) = await ResolveVersionAsync(setCode, scopeKey, null, null, ct);
        var items = version.Values
            .Where(value => !value.IsDeprecated)
            .Where(value => IsValueEffectiveAt(value, effectiveAt))
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.ValueCode, StringComparer.OrdinalIgnoreCase)
            .Select(value => new BusinessReferenceDataPublishedValueItemModel(
                value.ValueCode,
                value.DisplayName,
                value.Description,
                !value.IsDeprecated,
                value.SortOrder,
                value.Attributes))
            .ToList();

        return new BusinessReferenceDataPublishedValuesModel(
            set.SetCode,
            version.VersionNumber,
            version.PublishedAt,
            items);
    }

    public async Task<BusinessReferenceDataValuesLookupModel> GetValuesAsync(
        string setCode,
        string? scopeKey,
        int? versionNumber,
        DateTimeOffset? asOfDate,
        bool includeDeprecated,
        bool includeAttributes,
        bool includeMappings,
        CancellationToken ct = default)
    {
        var (set, version, effectiveAt) = await ResolveVersionAsync(setCode, scopeKey, versionNumber, asOfDate, ct);
        var values = SelectValues(version, includeDeprecated, includeAttributes, effectiveAt);

        var attributeCodes = includeAttributes
            ? version.AttributeDefinitions
                .Select(x => x.AttributeCode)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var mappings = includeMappings
            ? version.Mappings
                .Select(x => new BusinessReferenceDataMappingModel(x.MappingKey, x.SourceValueCode, x.TargetCode, x.TargetLabel))
                .ToList()
            : [];

        return new BusinessReferenceDataValuesLookupModel(
            set.SetCode,
            set.ScopeType,
            NormalizeOptional(scopeKey),
            version.VersionNumber,
            version.BusinessReferenceDataVersionId,
            version.PublishedAt,
            values,
            attributeCodes,
            mappings);
    }

    public async Task<BusinessReferenceDataHierarchyLookupModel> GetHierarchyAsync(
        string setCode,
        string? scopeKey,
        int? versionNumber,
        DateTimeOffset? asOfDate,
        bool includeDeprecated,
        bool includeAttributes,
        bool includeMappings,
        CancellationToken ct = default)
    {
        var (set, version, effectiveAt) = await ResolveVersionAsync(setCode, scopeKey, versionNumber, asOfDate, ct);
        var flattened = SelectValues(version, includeDeprecated, includeAttributes, effectiveAt);
        var tree = BuildTree(flattened);

        var attributeCodes = includeAttributes
            ? version.AttributeDefinitions
                .Select(x => x.AttributeCode)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var mappings = includeMappings
            ? version.Mappings
                .Select(x => new BusinessReferenceDataMappingModel(x.MappingKey, x.SourceValueCode, x.TargetCode, x.TargetLabel))
                .ToList()
            : [];

        return new BusinessReferenceDataHierarchyLookupModel(
            set.SetCode,
            set.ScopeType,
            NormalizeOptional(scopeKey),
            version.VersionNumber,
            version.BusinessReferenceDataVersionId,
            version.PublishedAt,
            flattened,
            tree,
            attributeCodes,
            mappings);
    }

    public async Task<BusinessReferenceDataUsageRegistrationResultModel> RegisterUsageAsync(
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
        CancellationToken ct = default)
    {
        _ = correlationId;
        var normalizedMode = NormalizeResolutionMode(resolutionMode);
        var targetVersionPin = normalizedMode == "pinned" ? versionPin : null;
        var targetAsOfDate = normalizedMode == "as-of" ? asOfDate : null;

        if (normalizedMode == "pinned" && (!versionPin.HasValue || versionPin.Value <= 0))
        {
            throw new InvalidOperationException("version_pin_required");
        }

        if (normalizedMode == "as-of" && !asOfDate.HasValue)
        {
            throw new InvalidOperationException("as_of_date_required");
        }

        var normalizedSetCode = setCode.Trim();
        var (set, resolvedVersion, _) = await ResolveVersionAsync(
            normalizedSetCode,
            scopeKey,
            targetVersionPin,
            targetAsOfDate,
            ct);

        var normalizedScopeType = NormalizeScopeType(set.ScopeType);
        var requestedScopeType = NormalizeOptional(scopeType);
        if (!string.IsNullOrWhiteSpace(requestedScopeType)
            && !string.Equals(requestedScopeType, normalizedScopeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("scope_type_mismatch");
        }

        var registration = new BusinessReferenceDataUsageRegistration
        {
            UsageRegistrationId = Guid.NewGuid(),
            TenantId = set.TenantId,
            SetCode = normalizedSetCode,
            ConsumerModule = consumerModule.Trim(),
            ConsumerName = consumerName.Trim(),
            ConsumerEndpoint = NormalizeOptional(consumerEndpoint),
            ScopeType = normalizedScopeType,
            ScopeKey = NormalizeOptional(scopeKey),
            VersionPin = targetVersionPin,
            AsOfDate = targetAsOfDate,
            ResolutionMode = normalizedMode,
            Criticality = NormalizeCriticality(criticality),
            Notes = NormalizeOptional(notes),
            LastResolvedVersionId = resolvedVersion.BusinessReferenceDataVersionId,
            LastResolvedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorId,
            UpdatedBy = actorId,
            IsActive = true
        };

        var upserted = await _repository.UpsertUsageRegistrationAsync(registration, ct);
        var summary = await _repository.GetUsageImpactSummaryAsync(normalizedSetCode, ct);
        if (summary.LastRegisteredAt.HasValue)
        {
            await _repository.UpdateSetUsageSummaryAsync(
                normalizedSetCode,
                summary.TotalRegistrations,
                summary.CriticalRegistrations,
                summary.LastRegisteredAt.Value,
                ct);
        }

        return new BusinessReferenceDataUsageRegistrationResultModel(
            upserted.UsageRegistrationId,
            upserted.SetCode,
            upserted.ConsumerModule,
            upserted.ConsumerName,
            upserted.ScopeType,
            upserted.ScopeKey,
            upserted.VersionPin,
            upserted.AsOfDate,
            upserted.ResolutionMode,
            upserted.Criticality,
            upserted.LastResolvedVersionId,
            upserted.LastResolvedAt,
            new BusinessReferenceDataUsageImpactSummaryModel(
                summary.TotalRegistrations,
                summary.CriticalRegistrations,
                summary.HighRegistrations,
                summary.MediumRegistrations,
                summary.LowRegistrations,
                summary.LastRegisteredAt));
    }

    public async Task<BusinessReferenceDataUsageRegistrationListModel> GetUsageRegistrationsAsync(
        string setCode,
        CancellationToken ct = default)
    {
        var normalizedSetCode = setCode.Trim();
        _ = await _repository.GetSetByCodeAsync(normalizedSetCode, ct)
            ?? throw new KeyNotFoundException("reference_data_set_not_found");

        var items = await _repository.GetUsageRegistrationsAsync(normalizedSetCode, ct);
        var summary = await _repository.GetUsageImpactSummaryAsync(normalizedSetCode, ct);
        var models = items.Select(x => new BusinessReferenceDataUsageRegistrationListItemModel(
            x.UsageRegistrationId,
            x.SetCode,
            x.ConsumerModule,
            x.ConsumerName,
            x.ConsumerEndpoint,
            x.ScopeType,
            x.ScopeKey,
            x.VersionPin,
            x.AsOfDate,
            x.ResolutionMode,
            x.Criticality,
            x.LastResolvedVersionId,
            x.LastResolvedAt,
            x.IsActive,
            x.CreatedAt,
            x.UpdatedAt)).ToList();

        return new BusinessReferenceDataUsageRegistrationListModel(
            normalizedSetCode,
            models,
            new BusinessReferenceDataUsageImpactSummaryModel(
                summary.TotalRegistrations,
                summary.CriticalRegistrations,
                summary.HighRegistrations,
                summary.MediumRegistrations,
                summary.LowRegistrations,
                summary.LastRegisteredAt));
    }

    public async Task<bool> DeactivateUsageRegistrationAsync(
        Guid usageRegistrationId,
        string actorId,
        string correlationId,
        CancellationToken ct = default)
    {
        _ = correlationId;
        var registration = await _repository.GetUsageRegistrationByIdAsync(usageRegistrationId, ct)
            ?? throw new KeyNotFoundException("usage_registration_not_found");

        if (!registration.IsActive)
        {
            return false;
        }

        var changed = await _repository.DeactivateUsageRegistrationAsync(usageRegistrationId, actorId, ct);
        if (!changed)
        {
            return false;
        }

        var summary = await _repository.GetUsageImpactSummaryAsync(registration.SetCode, ct);
        if (summary.LastRegisteredAt.HasValue)
        {
            await _repository.UpdateSetUsageSummaryAsync(
                registration.SetCode,
                summary.TotalRegistrations,
                summary.CriticalRegistrations,
                summary.LastRegisteredAt.Value,
                ct);
        }

        return true;
    }

    public async Task<int> DeactivateUsageRegistrationsBulkAsync(
        IReadOnlyCollection<Guid> usageRegistrationIds,
        string actorId,
        string correlationId,
        CancellationToken ct = default)
    {
        _ = correlationId;
        if (usageRegistrationIds is null || usageRegistrationIds.Count == 0)
        {
            return 0;
        }

        var affectedSetCodes = new HashSet<string>(StringComparer.Ordinal);
        var deactivatedCount = 0;

        foreach (var usageRegistrationId in usageRegistrationIds.Distinct())
        {
            var registration = await _repository.GetUsageRegistrationByIdAsync(usageRegistrationId, ct);
            if (registration is null || !registration.IsActive)
            {
                continue;
            }

            var changed = await _repository.DeactivateUsageRegistrationAsync(usageRegistrationId, actorId, ct);
            if (!changed)
            {
                continue;
            }

            deactivatedCount++;
            affectedSetCodes.Add(registration.SetCode);
        }

        foreach (var setCode in affectedSetCodes)
        {
            var summary = await _repository.GetUsageImpactSummaryAsync(setCode, ct);
            if (summary.LastRegisteredAt.HasValue)
            {
                await _repository.UpdateSetUsageSummaryAsync(
                    setCode,
                    summary.TotalRegistrations,
                    summary.CriticalRegistrations,
                    summary.LastRegisteredAt.Value,
                    ct);
            }
        }

        return deactivatedCount;
    }

    private async Task<(BusinessReferenceDataSet Set, BusinessReferenceDataVersion Version, DateTimeOffset EffectiveAt)> ResolveVersionAsync(
        string setCode,
        string? scopeKey,
        int? versionNumber,
        DateTimeOffset? asOfDate,
        CancellationToken ct)
    {
        var normalizedSetCode = setCode.Trim();
        var set = await _repository.GetSetByCodeAsync(normalizedSetCode, ct)
            ?? throw new KeyNotFoundException("reference_data_set_not_found");
        if (set.Status == BusinessReferenceDataSetStatus.Retired)
        {
            throw new InvalidOperationException("reference_data_set_retired");
        }

        var normalizedScopeType = NormalizeScopeType(set.ScopeType);
        var normalizedScopeKey = NormalizeOptional(scopeKey);
        ValidateScopeRule(normalizedScopeType, normalizedScopeKey);

        var publishedVersions = (await _repository.GetPublishedVersionsBySetCodeAsync(normalizedSetCode, ct))
            .Where(x => x.Status == BusinessReferenceDataVersionStatus.Published)
            .ToList();
        if (publishedVersions.Count == 0)
        {
            throw new InvalidOperationException("no_published_version");
        }

        var effectiveAt = asOfDate ?? DateTimeOffset.UtcNow;
        var scopedVersions = publishedVersions
            .Where(x => MatchesScope(x, normalizedScopeType, normalizedScopeKey))
            .ToList();

        if (scopedVersions.Count == 0)
        {
            throw new KeyNotFoundException("scope_not_found");
        }

        BusinessReferenceDataVersion? selected;
        if (versionNumber.HasValue)
        {
            selected = scopedVersions.FirstOrDefault(x => x.VersionNumber == versionNumber.Value);
            if (selected is null)
            {
                throw new KeyNotFoundException("published_version_not_found");
            }
        }
        else
        {
            selected = scopedVersions
                .Where(x => IsEffectiveAt(x, effectiveAt))
                .OrderByDescending(x => x.VersionNumber)
                .FirstOrDefault();

            if (selected is null)
            {
                throw new KeyNotFoundException("effective_version_not_found");
            }
        }

        return (set, selected, effectiveAt);
    }

    private static List<BusinessReferenceDataConsumerValueModel> SelectValues(
        BusinessReferenceDataVersion version,
        bool includeDeprecated,
        bool includeAttributes,
        DateTimeOffset effectiveAt)
    {
        return version.Values
            .Where(value => includeDeprecated || !value.IsDeprecated)
            .Where(value => IsValueEffectiveAt(value, effectiveAt))
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.ValueCode, StringComparer.OrdinalIgnoreCase)
            .Select(value => new BusinessReferenceDataConsumerValueModel(
                value.ValueCode,
                value.DisplayName,
                value.Description,
                value.IsDeprecated,
                value.ReplacementValueCode,
                value.ParentValueCode,
                value.SortOrder,
                value.EffectiveFrom,
                value.EffectiveTo,
                includeAttributes ? value.Attributes : null))
            .ToList();
    }

    private static List<BusinessReferenceDataHierarchyNodeModel> BuildTree(IReadOnlyList<BusinessReferenceDataConsumerValueModel> values)
    {
        const string root = "__root__";
        var byParent = values
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ParentValueCode) ? root : x.ParentValueCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        List<BusinessReferenceDataHierarchyNodeModel> BuildChildren(string parentCode)
        {
            if (!byParent.TryGetValue(parentCode, out var children))
            {
                return [];
            }

            return children
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.ValueCode, StringComparer.OrdinalIgnoreCase)
                .Select(x => new BusinessReferenceDataHierarchyNodeModel(
                    x.ValueCode,
                    x.DisplayName,
                    x.IsDeprecated,
                    BuildChildren(x.ValueCode)))
                .ToList();
        }

        return BuildChildren(root);
    }

    private static bool MatchesScope(BusinessReferenceDataVersion version, string scopeType, string? scopeKey)
    {
        if (scopeType == "global")
        {
            return true;
        }

        return string.Equals(version.ScopeKey, scopeKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEffectiveAt(BusinessReferenceDataVersion version, DateTimeOffset point)
    {
        var publishedAt = version.PublishedAt ?? version.CreatedAt;
        if (publishedAt > point)
        {
            return false;
        }

        if (version.EffectiveFrom.HasValue && version.EffectiveFrom.Value > point)
        {
            return false;
        }

        if (version.EffectiveTo.HasValue && version.EffectiveTo.Value <= point)
        {
            return false;
        }

        return true;
    }

    private static bool IsValueEffectiveAt(BusinessReferenceDataValue value, DateTimeOffset point)
    {
        if (value.EffectiveFrom.HasValue && value.EffectiveFrom.Value > point)
        {
            return false;
        }

        if (value.EffectiveTo.HasValue && value.EffectiveTo.Value <= point)
        {
            return false;
        }

        return true;
    }

    private static void ValidateScopeRule(string scopeType, string? scopeKey)
    {
        if (!AllowedScopeTypes.Contains(scopeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("unsupported_scope_type");
        }

        if (scopeType == "global")
        {
            if (!string.IsNullOrWhiteSpace(scopeKey))
            {
                throw new InvalidOperationException("scope_key_not_allowed_for_global");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            throw new InvalidOperationException("scope_key_required");
        }
    }

    private static string NormalizeScopeType(string scopeType)
    {
        return scopeType.Trim().ToLowerInvariant();
    }

    private static string NormalizeResolutionMode(string? resolutionMode)
    {
        var mode = string.IsNullOrWhiteSpace(resolutionMode) ? "latest" : resolutionMode.Trim().ToLowerInvariant();
        if (!AllowedResolutionModes.Contains(mode, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("invalid_resolution_mode");
        }

        return mode;
    }

    private static string NormalizeCriticality(string? criticality)
    {
        var value = string.IsNullOrWhiteSpace(criticality) ? "medium" : criticality.Trim().ToLowerInvariant();
        if (!AllowedCriticalities.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("invalid_criticality");
        }

        return value;
    }

    private static string? NormalizeOptional(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }
}
