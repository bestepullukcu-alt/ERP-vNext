using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataCatalogLoaderService
{
    Task<BusinessReferenceDataCatalogLoadSummary> LoadFromFileAsync(
        string filePath,
        Guid tenantId,
        string actorId,
        IReadOnlyList<string> requiredSetCodes,
        CancellationToken ct = default);
}

public sealed class BusinessReferenceDataCatalogLoaderService : IBusinessReferenceDataCatalogLoaderService
{
    private const string CorrelationPrefix = "BusinessReferenceData-catalog-load";
    private const string OverrideReason = "BusinessReferenceData catalog load override";

    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly IBusinessReferenceDataPublishService _publishService;
    private readonly ITenantContext _tenantContext;

    public BusinessReferenceDataCatalogLoaderService(
        IBusinessReferenceDataStewardshipRepository repository,
        IBusinessReferenceDataPublishService publishService,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _publishService = publishService;
        _tenantContext = tenantContext;
    }

    public async Task<BusinessReferenceDataCatalogLoadSummary> LoadFromFileAsync(
        string filePath,
        Guid tenantId,
        string actorId,
        IReadOnlyList<string> requiredSetCodes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("catalog_path_required");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("catalog_file_not_found", filePath);
        }

        var payload = await File.ReadAllTextAsync(filePath, ct);
        var catalog = Parse(payload);
        if (!string.Equals(catalog.Module, "BusinessReferenceData", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("invalid_catalog_module");
        }

        var summary = new BusinessReferenceDataCatalogLoadSummary
        {
            FilePath = Path.GetFullPath(filePath),
            CatalogVersion = catalog.CatalogVersion ?? "unknown",
            Module = catalog.Module ?? "BusinessReferenceData"
        };

        using var _ = TenantScope.Begin(_tenantContext, tenantId);

        var normalizedSets = (catalog.Sets ?? [])
            .Where(x => x is not null)
            .Select(NormalizeSet)
            .ToList();

        summary.SetsProcessed = normalizedSets.Count;

        foreach (var setDoc in normalizedSets)
        {
            ct.ThrowIfCancellationRequested();
            var setCode = setDoc.SetCode!;
            var setName = setDoc.SetName!;
            var scopeType = setDoc.ScopeType!;

            var setCorrelationId = BuildCorrelationId(setCode);
            var valueCollision = FindDuplicateValueCode(setDoc.Values);
            if (valueCollision is not null)
            {
                summary.BlockedConflicts.Add($"set_code={setCode}: duplicate value_code '{valueCollision}'.");
                continue;
            }

            var set = await _repository.GetSetByCodeAsync(setCode, ct);
            if (set is not null && !string.Equals(set.ScopeType, scopeType, StringComparison.OrdinalIgnoreCase))
            {
                summary.BlockedConflicts.Add(
                    $"set_code={setCode}: existing scope_type '{set.ScopeType}' conflicts with catalog scope_type '{scopeType}'.");
                continue;
            }

            if (set is null)
            {
                set = new BusinessReferenceDataSet
                {
                    TenantId = tenantId,
                    BusinessReferenceDataSetId = Guid.NewGuid(),
                    SetCode = setCode,
                    Name = setName,
                    ScopeType = scopeType,
                    Description = setDoc.Description,
                    Status = ParseSetStatus(setDoc.Status),
                    CreatedBy = actorId,
                    LastCorrelationId = setCorrelationId
                };

                await _repository.CreateSetAsync(set, ct);
                summary.SetsInserted++;
            }
            else
            {
                var changed = false;
                if (!string.Equals(set.Name, setName, StringComparison.Ordinal))
                {
                    set.Name = setName;
                    changed = true;
                }

                if (!string.Equals(set.Description, setDoc.Description, StringComparison.Ordinal))
                {
                    set.Description = setDoc.Description;
                    changed = true;
                }

                var parsedStatus = ParseSetStatus(setDoc.Status);
                if (set.Status != parsedStatus)
                {
                    set.Status = parsedStatus;
                    changed = true;
                }

                if (changed)
                {
                    set.UpdatedBy = actorId;
                    set.LastCorrelationId = setCorrelationId;
                    var updated = await _repository.UpdateSetAsync(set, set.RowVersion, ct);
                    if (!updated)
                    {
                        throw new InvalidOperationException("set_concurrency_conflict");
                    }

                    summary.SetsUpdated++;
                }
            }

            var versions = await _repository.GetVersionsBySetIdAsync(set.BusinessReferenceDataSetId, ct);
            var idempotencyKey = BuildIdempotencyKey(summary.CatalogVersion, setCode);
            var alreadyLoaded = versions.FirstOrDefault(x =>
                x.Status == BusinessReferenceDataVersionStatus.Published
                && string.Equals(x.LastPublishIdempotencyKey, idempotencyKey, StringComparison.Ordinal));

            if (alreadyLoaded is not null)
            {
                await EnsureSetPointersAsync(set, alreadyLoaded.BusinessReferenceDataVersionId, actorId, setCorrelationId, ct);
                summary.SetsAlreadyLoaded++;
                summary.ValuesUnchanged += alreadyLoaded.Values.Count;
                continue;
            }

            var draft = versions
                .Where(x => x.Status == BusinessReferenceDataVersionStatus.Draft && !x.IsDeleted)
                .OrderByDescending(x => x.VersionNumber)
                .FirstOrDefault();

            if (draft is null)
            {
                draft = new BusinessReferenceDataVersion
                {
                    TenantId = tenantId,
                    BusinessReferenceDataVersionId = Guid.NewGuid(),
                    BusinessReferenceDataSetId = set.BusinessReferenceDataSetId,
                    VersionNumber = await _repository.GetNextVersionNumberAsync(set.BusinessReferenceDataSetId, ct),
                    Status = BusinessReferenceDataVersionStatus.Draft,
                    ConcurrencyToken = Guid.NewGuid().ToString("N"),
                    IsImmutable = false,
                    BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Draft,
                    BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.NotStarted,
                    IsEditable = true,
                    CreatedBy = actorId,
                    LastCorrelationId = setCorrelationId
                };

                await _repository.CreateVersionAsync(draft, ct);
                set.ActiveDraftVersionId = draft.BusinessReferenceDataVersionId;
                set.UpdatedBy = actorId;
                set.LastCorrelationId = setCorrelationId;
                var setUpdated = await _repository.UpdateSetAsync(set, set.RowVersion, ct);
                if (!setUpdated)
                {
                    throw new InvalidOperationException("set_concurrency_conflict");
                }
            }

            var existingByCode = draft.Values.ToDictionary(x => x.ValueCode, StringComparer.OrdinalIgnoreCase);
            var mappedValues = setDoc.Values
                .Select(x => new BusinessReferenceDataValue
                {
                    ValueCode = x.ValueCode!,
                    DisplayName = x.DisplayName!,
                    Description = x.Description,
                    IsDeprecated = !x.IsActive,
                    SortOrder = x.SortOrder
                })
                .ToList();

            draft.Values = mappedValues;
            draft.DeprecatedValuesEffectiveCount = mappedValues.Count(x => x.IsDeprecated);
            draft.UpdatedBy = actorId;
            draft.LastCorrelationId = setCorrelationId;

            var saveOk = await _repository.UpdateVersionAsync(draft, draft.ConcurrencyToken, ct);
            if (!saveOk)
            {
                throw new InvalidOperationException("version_concurrency_conflict");
            }

            summary.ValuesInserted += mappedValues.Count(x => !existingByCode.ContainsKey(x.ValueCode));
            summary.ValuesUpdated += mappedValues.Count(x => existingByCode.ContainsKey(x.ValueCode));

            var published = await _publishService.PublishAsync(
                draft.BusinessReferenceDataVersionId,
                actorId,
                setCorrelationId,
                idempotencyKey,
                "Immediate",
                null,
                draft.ConcurrencyToken,
                overrideAction: true,
                overrideReason: OverrideReason,
                ct);

            await EnsureSetPointersAsync(set, published.VersionId, actorId, setCorrelationId, ct);
            summary.SetsLoaded++;
        }

        var required = requiredSetCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var setCode in required)
        {
            var versions = await _repository.GetPublishedVersionsBySetCodeAsync(setCode, ct);
            var version = versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            if (version is null)
            {
                summary.LookupResults.Add(new BusinessReferenceDataLookupVerification(setCode, null, false, false, "published_set_not_found"));
                continue;
            }

            var sourceSet = normalizedSets.FirstOrDefault(x => string.Equals(x.SetCode, setCode, StringComparison.OrdinalIgnoreCase));
            var sampleValueCode = sourceSet?.Values.FirstOrDefault()?.ValueCode;
            var valueFound = string.IsNullOrWhiteSpace(sampleValueCode)
                || version.Values.Any(v => string.Equals(v.ValueCode, sampleValueCode, StringComparison.OrdinalIgnoreCase));

            summary.LookupResults.Add(new BusinessReferenceDataLookupVerification(
                setCode,
                sampleValueCode,
                true,
                valueFound,
                valueFound ? "ok" : "sample_value_not_found"));
        }

        return summary;
    }

    private async Task EnsureSetPointersAsync(
        BusinessReferenceDataSet set,
        Guid publishedVersionId,
        string actorId,
        string correlationId,
        CancellationToken ct)
    {
        var latestSet = await _repository.GetSetByCodeAsync(set.SetCode, ct)
            ?? throw new InvalidOperationException("set_not_found_after_publish");

        set = latestSet;
        if (set.PublishedVersionId == publishedVersionId
            && set.ActiveDraftVersionId is null
            && set.Status == BusinessReferenceDataSetStatus.Active)
        {
            return;
        }

        set.PublishedVersionId = publishedVersionId;
        set.ActiveDraftVersionId = null;
        set.Status = BusinessReferenceDataSetStatus.Active;
        set.UpdatedBy = actorId;
        set.LastCorrelationId = correlationId;
        var ok = await _repository.UpdateSetAsync(set, set.RowVersion, ct);
        if (!ok)
        {
            throw new InvalidOperationException("set_concurrency_conflict");
        }
    }

    private static string? FindDuplicateValueCode(IReadOnlyList<BusinessReferenceDataCatalogValueDocument> values)
    {
        return values
            .GroupBy(x => x.ValueCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1)
            ?.Key;
    }

    private static string BuildCorrelationId(string setCode)
        => $"{CorrelationPrefix}:{setCode}:{Guid.NewGuid():N}";

    private static string BuildIdempotencyKey(string catalogVersion, string setCode)
        => $"BusinessReferenceData-catalog-v{catalogVersion}:{setCode}".ToLowerInvariant();

    private static BusinessReferenceDataCatalogDocument Parse(string payload)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var document = JsonSerializer.Deserialize<BusinessReferenceDataCatalogDocument>(payload, options);
        if (document is null)
        {
            throw new InvalidOperationException("catalog_parse_failed");
        }

        return document;
    }

    private static BusinessReferenceDataCatalogSetDocument NormalizeSet(BusinessReferenceDataCatalogSetDocument raw)
    {
        var setCode = raw.SetCode?.Trim();
        var setName = raw.SetName?.Trim();
        var scopeType = raw.ScopeType?.Trim();
        if (string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(setName) || string.IsNullOrWhiteSpace(scopeType))
        {
            throw new InvalidOperationException("catalog_set_shape_mismatch");
        }

        var values = (raw.Values ?? [])
            .Select(value =>
            {
                var code = value.ValueCode?.Trim();
                var name = value.DisplayName?.Trim();
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                {
                    throw new InvalidOperationException($"catalog_value_shape_mismatch:{setCode}");
                }

                return new BusinessReferenceDataCatalogValueDocument(
                    code,
                    name,
                    string.IsNullOrWhiteSpace(value.Description) ? null : value.Description.Trim(),
                    value.IsActive,
                    value.SortOrder);
            })
            .ToList();

        return new BusinessReferenceDataCatalogSetDocument(
            setCode,
            setName,
            scopeType,
            string.IsNullOrWhiteSpace(raw.Status) ? "Active" : raw.Status.Trim(),
            string.IsNullOrWhiteSpace(raw.Description) ? null : raw.Description.Trim(),
            values);
    }

    private static BusinessReferenceDataSetStatus ParseSetStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return BusinessReferenceDataSetStatus.Active;
        }

        if (Enum.TryParse<BusinessReferenceDataSetStatus>(raw, true, out var parsed))
        {
            return parsed;
        }

        return BusinessReferenceDataSetStatus.Active;
    }
}

public sealed record BusinessReferenceDataCatalogDocument(
    [property: JsonPropertyName("catalog_version")] string? CatalogVersion,
    [property: JsonPropertyName("module")] string? Module,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("sets")] IReadOnlyList<BusinessReferenceDataCatalogSetDocument> Sets)
{
    public BusinessReferenceDataCatalogDocument() : this(null, null, null, []) { }
}

public sealed record BusinessReferenceDataCatalogSetDocument(
    [property: JsonPropertyName("set_code")] string? SetCode,
    [property: JsonPropertyName("set_name")] string? SetName,
    [property: JsonPropertyName("scope_type")] string? ScopeType,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("values")] IReadOnlyList<BusinessReferenceDataCatalogValueDocument> Values)
{
    public BusinessReferenceDataCatalogSetDocument() : this(null, null, null, null, null, []) { }
}

public sealed record BusinessReferenceDataCatalogValueDocument(
    [property: JsonPropertyName("value_code")] string? ValueCode,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("sort_order")] int SortOrder)
{
    public BusinessReferenceDataCatalogValueDocument() : this(null, null, null, true, 0) { }
}

public sealed class BusinessReferenceDataCatalogLoadSummary
{
    public string FilePath { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public int SetsProcessed { get; set; }
    public int SetsInserted { get; set; }
    public int SetsUpdated { get; set; }
    public int SetsLoaded { get; set; }
    public int SetsAlreadyLoaded { get; set; }
    public int ValuesInserted { get; set; }
    public int ValuesUpdated { get; set; }
    public int ValuesUnchanged { get; set; }
    public List<string> BlockedConflicts { get; } = [];
    public List<BusinessReferenceDataLookupVerification> LookupResults { get; } = [];
}

public sealed record BusinessReferenceDataLookupVerification(
    string SetCode,
    string? SampleValueCode,
    bool SetFound,
    bool ValueFound,
    string Status);
