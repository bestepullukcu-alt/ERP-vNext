using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
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

    Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedGskuCatalogFromFileAsync(
        string filePath,
        string actorId,
        IReadOnlyList<string> requiredSetCodes,
        CancellationToken ct = default);

    Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedMarketCatalogFromFileAsync(
        string filePath,
        string actorId,
        CancellationToken ct = default);

    Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedMarketCatalogFromFileAsync(
        string filePath, string actorId, string idempotencyNamespace,
        IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization,
        VerifiedMarketOperationalFacts facts, CancellationToken ct = default);

    Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedGskuCatalogFromFileAsync(
        string filePath,
        string actorId,
        string idempotencyNamespace,
        IReadOnlyList<string> requiredSetCodes,
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization authorization,
        VerifiedGskuOperationalFacts facts,
        CancellationToken ct = default);
}

public sealed class BusinessReferenceDataCatalogLoaderService : IBusinessReferenceDataCatalogLoaderService
{
    private const string CorrelationPrefix = "BusinessReferenceData-catalog-load";
    private const string OverrideReason = "BusinessReferenceData catalog load override";

    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly IBusinessReferenceDataPublishService _publishService;
    private readonly ITenantContext _tenantContext;
    private readonly IBusinessReferenceDataVerifiedGskuOperationalEligibility? _operationalEligibility;
    private readonly IBusinessReferenceDataVerifiedMarketOperationalEligibility? _marketOperationalEligibility;

    public BusinessReferenceDataCatalogLoaderService(
        IBusinessReferenceDataStewardshipRepository repository,
        IBusinessReferenceDataPublishService publishService,
        ITenantContext tenantContext,
        IBusinessReferenceDataVerifiedGskuOperationalEligibility? operationalEligibility = null,
        IBusinessReferenceDataVerifiedMarketOperationalEligibility? marketOperationalEligibility = null)
    {
        _repository = repository;
        _publishService = publishService;
        _tenantContext = tenantContext;
        _operationalEligibility = operationalEligibility;
        _marketOperationalEligibility = marketOperationalEligibility;
    }

    public Task<BusinessReferenceDataCatalogLoadSummary> LoadFromFileAsync(
        string filePath,
        Guid tenantId,
        string actorId,
        IReadOnlyList<string> requiredSetCodes,
        CancellationToken ct = default)
        => LoadCoreAsync(filePath, tenantId, actorId, requiredSetCodes, verifiedGsku: false, verifiedMarket: false, null, null, ct);

    public Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedGskuCatalogFromFileAsync(
        string filePath,
        string actorId,
        IReadOnlyList<string> requiredSetCodes,
        CancellationToken ct = default)
        => LoadCoreAsync(filePath, Guid.Empty, actorId, requiredSetCodes, verifiedGsku: true, verifiedMarket: false, null, null, ct);

    public Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedMarketCatalogFromFileAsync(
        string filePath,
        string actorId,
        CancellationToken ct = default)
        => LoadCoreAsync(
            filePath,
            Guid.Empty,
            actorId,
            [VerifiedMarketCatalogContract.SetCode],
            verifiedGsku: false,
            verifiedMarket: true,
            null,
            null,
            ct);

    public Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedMarketCatalogFromFileAsync(
        string filePath, string actorId, string idempotencyNamespace,
        IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization,
        VerifiedMarketOperationalFacts facts, CancellationToken ct = default)
    {
        if (_marketOperationalEligibility is null || !_marketOperationalEligibility.IsAuthorized(authorization, facts)
            || !string.Equals(Path.GetFullPath(filePath), facts.CatalogPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(actorId.Trim(), facts.ActorId, StringComparison.Ordinal)
            || !string.Equals(idempotencyNamespace.Trim(), facts.IdempotencyNamespace, StringComparison.Ordinal))
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        return LoadCoreAsync(filePath, facts.ReferenceTenantId, actorId, [VerifiedMarketCatalogContract.SetCode], false, true, null, null, ct, authorization, facts, idempotencyNamespace);
    }

    public Task<BusinessReferenceDataCatalogLoadSummary> LoadVerifiedGskuCatalogFromFileAsync(
        string filePath,
        string actorId,
        string idempotencyNamespace,
        IReadOnlyList<string> requiredSetCodes,
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization authorization,
        VerifiedGskuOperationalFacts facts,
        CancellationToken ct = default)
    {
        if (_operationalEligibility is null
            || !_operationalEligibility.IsAuthorized(authorization, facts)
            || !string.Equals(Path.GetFullPath(filePath), facts.CatalogPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(actorId.Trim(), facts.ActorId, StringComparison.Ordinal)
            || !string.Equals(idempotencyNamespace.Trim(), facts.IdempotencyNamespace, StringComparison.Ordinal)
            || !requiredSetCodes.SequenceEqual(facts.RequiredSetCodes, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        return LoadCoreAsync(filePath, facts.ReferenceTenantId, actorId, requiredSetCodes, verifiedGsku: true, verifiedMarket: false, authorization, facts, ct);
    }

    private async Task<BusinessReferenceDataCatalogLoadSummary> LoadCoreAsync(
        string filePath,
        Guid tenantId,
        string actorId,
        IReadOnlyList<string> requiredSetCodes,
        bool verifiedGsku,
        bool verifiedMarket,
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization? authorization,
        VerifiedGskuOperationalFacts? operationalFacts,
        CancellationToken ct,
        IBusinessReferenceDataVerifiedMarketOperationalAuthorization? marketAuthorization = null,
        VerifiedMarketOperationalFacts? marketFacts = null,
        string? marketIdempotencyNamespace = null)
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
        var verifiedCatalog = verifiedGsku || verifiedMarket;
        if (verifiedCatalog)
        {
            ValidateNoDuplicateJsonProperties(payload);
        }

        var catalog = Parse(payload);
        if (!string.Equals(catalog.Module, "BusinessReferenceData", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("invalid_catalog_module");
        }

        if (!verifiedCatalog && IsVerifiedGskuCatalog(filePath, catalog))
        {
            throw new InvalidOperationException("VERIFIED_GSKU_CATALOG_CONTRACT_REQUIRED");
        }
        if (!verifiedCatalog && IsVerifiedMarketCatalog(catalog))
        {
            throw new InvalidOperationException("VERIFIED_MARKET_CATALOG_CONTRACT_REQUIRED");
        }

        if (verifiedCatalog && string.IsNullOrWhiteSpace(catalog.CatalogVersion))
        {
            throw new InvalidOperationException("catalog_version_required");
        }

        var catalogVersion = string.IsNullOrWhiteSpace(catalog.CatalogVersion)
            ? "unknown"
            : catalog.CatalogVersion.Trim();
        var catalogFingerprint = verifiedCatalog
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()
            : string.Empty;
        if (verifiedCatalog)
        {
            tenantId = _repository.GetRequiredReferenceTenantId();
        }

        if (operationalFacts is not null
            && (tenantId != operationalFacts.ReferenceTenantId
                || !string.Equals(catalogVersion, operationalFacts.CatalogVersion, StringComparison.Ordinal)
                || !string.Equals(catalogFingerprint, operationalFacts.CatalogFingerprint, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFullPath(filePath), operationalFacts.CatalogPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actorId.Trim(), operationalFacts.ActorId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        var summary = new BusinessReferenceDataCatalogLoadSummary
        {
            FilePath = Path.GetFullPath(filePath),
            CatalogVersion = catalogVersion,
            CatalogFingerprint = catalogFingerprint,
            Module = catalog.Module ?? "BusinessReferenceData"
        };

        using var _ = TenantScope.Begin(_tenantContext, tenantId);

        var normalizedSets = (catalog.Sets ?? [])
            .Where(x => x is not null)
            .Select(NormalizeSet)
            .ToList();

        if (verifiedGsku)
        {
            ValidateLockedGskuCatalog(catalog, normalizedSets);
        }
        else if (verifiedMarket)
        {
            ValidateRawMarketCatalog(catalog);
            ValidateLockedMarketCatalog(catalog, normalizedSets);
        }

        summary.SetsProcessed = normalizedSets.Count;

        if (verifiedCatalog)
        {
            await PreflightCatalogAsync(normalizedSets, catalogVersion, catalogFingerprint, summary, verifiedMarket, ct);
            if (summary.BlockedConflicts.Count > 0)
            {
                return summary;
            }
        }

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
            var idempotencyKey = BuildIdempotencyKey(summary.CatalogVersion, setCode, marketIdempotencyNamespace);
            var existingOperation = verifiedCatalog
                ? await _repository.GetPublishOperationByIdempotencyKeyAsync(idempotencyKey, ct)
                : null;
            var alreadyLoaded = verifiedCatalog
                ? existingOperation is null
                    ? null
                    : versions.SingleOrDefault(x =>
                        x.BusinessReferenceDataVersionId == existingOperation.BusinessReferenceDataVersionId)
                : versions.FirstOrDefault(x =>
                    x.Status == BusinessReferenceDataVersionStatus.Published
                    && string.Equals(x.LastPublishIdempotencyKey, idempotencyKey, StringComparison.Ordinal));

            if (alreadyLoaded is not null)
            {
                if (verifiedCatalog)
                {
                    await PublishVerifiedAsync(
                        alreadyLoaded.BusinessReferenceDataVersionId,
                        actorId,
                        setCorrelationId,
                        idempotencyKey,
                        "Immediate",
                        null,
                        existingOperation!.ExpectedTargetVersionToken,
                        overrideAction: true,
                        overrideReason: OverrideReason,
                        authorization,
                        operationalFacts,
                        verifiedMarket,
                        ct,
                        marketAuthorization,
                        marketFacts,
                        marketIdempotencyNamespace);
                }
                else
                {
                    await EnsureSetPointersLegacyAsync(set, alreadyLoaded.BusinessReferenceDataVersionId, actorId, setCorrelationId, ct);
                }

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
                    SortOrder = x.SortOrder,
                    Attributes = x.Attributes.Count == 0
                        ? null
                        : new Dictionary<string, string>(x.Attributes, StringComparer.Ordinal)
                })
                .ToList();

            draft.Values = mappedValues;
            draft.AttributeDefinitions = setDoc.AttributeDefinitions
                .Select(x => new BusinessReferenceDataAttributeDefinition
                {
                    AttributeCode = x.AttributeCode!,
                    DisplayName = x.DisplayName!,
                    DataType = x.DataType!,
                    IsRequired = x.IsRequired
                })
                .ToList();
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

            if (verifiedCatalog)
            {
                var operationClaim = await _repository.CreateOrGetPublishOperationAsync(
                    new BusinessReferenceDataPublishOperation
                    {
                        TenantId = tenantId,
                        BusinessReferenceDataSetId = set.BusinessReferenceDataSetId,
                        BusinessReferenceDataVersionId = draft.BusinessReferenceDataVersionId,
                        IdempotencyKey = idempotencyKey,
                        ExpectedPublishedVersionId = set.PublishedVersionId,
                        ExpectedSetVersion = set.RowVersion,
                        ExpectedTargetVersionToken = draft.ConcurrencyToken,
                        CatalogVersion = catalogVersion,
                        CatalogFingerprint = catalogFingerprint,
                        CreatedBy = actorId
                    },
                    ct);
                if (operationClaim.Outcome == BusinessReferenceDataPublishOperationCreateOutcome.Conflict)
                {
                    throw new InvalidOperationException("REFERENCE_PUBLISH_CONFLICT");
                }
            }

            var published = verifiedCatalog
                ? await PublishVerifiedAsync(
                    draft.BusinessReferenceDataVersionId,
                    actorId,
                    setCorrelationId,
                    idempotencyKey,
                    "Immediate",
                    null,
                    draft.ConcurrencyToken,
                    overrideAction: true,
                    overrideReason: OverrideReason,
                    authorization,
                    operationalFacts,
                    verifiedMarket,
                    ct,
                    marketAuthorization,
                    marketFacts,
                    marketIdempotencyNamespace)
                : await _publishService.PublishAsync(
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
            if (!verifiedCatalog)
            {
                await EnsureSetPointersLegacyAsync(set, published.VersionId, actorId, setCorrelationId, ct);
            }

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

    private Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization? authorization,
        VerifiedGskuOperationalFacts? facts,
        bool verifiedMarket,
        CancellationToken ct,
        IBusinessReferenceDataVerifiedMarketOperationalAuthorization? marketAuthorization = null,
        VerifiedMarketOperationalFacts? marketFacts = null,
        string? marketIdempotencyNamespace = null) =>
        verifiedMarket
            ? marketAuthorization is not null && marketFacts is not null && !string.IsNullOrWhiteSpace(marketIdempotencyNamespace)
            ? _publishService.PublishVerifiedMarketAsync(
                versionId, actorId, correlationId, idempotencyKey, publishMode, publishAt,
                expectedConcurrencyToken, overrideAction, overrideReason, marketAuthorization, marketFacts, ct)
            : _publishService.PublishVerifiedMarketAsync(
                versionId, actorId, correlationId, idempotencyKey, publishMode, publishAt,
                expectedConcurrencyToken, overrideAction, overrideReason, ct)
            : authorization is not null && facts is not null
            ? _publishService.PublishVerifiedAsync(
                versionId, actorId, correlationId, idempotencyKey, publishMode, publishAt,
                expectedConcurrencyToken, overrideAction, overrideReason, authorization, facts, ct)
            : _publishService.PublishVerifiedAsync(
                versionId, actorId, correlationId, idempotencyKey, publishMode, publishAt,
                expectedConcurrencyToken, overrideAction, overrideReason, ct);

    private async Task EnsureSetPointersLegacyAsync(
        BusinessReferenceDataSet set,
        Guid publishedVersionId,
        string actorId,
        string correlationId,
        CancellationToken ct)
    {
        var latestSet = await _repository.GetSetByCodeAsync(set.SetCode, ct)
            ?? throw new InvalidOperationException("set_not_found_after_publish");
        if (latestSet.PublishedVersionId == publishedVersionId
            && latestSet.ActiveDraftVersionId is null
            && latestSet.Status == BusinessReferenceDataSetStatus.Active)
        {
            return;
        }

        latestSet.PublishedVersionId = publishedVersionId;
        latestSet.ActiveDraftVersionId = null;
        latestSet.Status = BusinessReferenceDataSetStatus.Active;
        latestSet.UpdatedBy = actorId;
        latestSet.LastCorrelationId = correlationId;
        if (!await _repository.UpdateSetAsync(latestSet, latestSet.RowVersion, ct))
        {
            throw new InvalidOperationException("set_concurrency_conflict");
        }
    }

    private static bool IsVerifiedGskuCatalog(string filePath, BusinessReferenceDataCatalogDocument catalog)
    {
        if (string.Equals(
                Path.GetFileName(filePath),
                "mod-0290-gsku-reference.json",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                catalog.Note?.Trim(),
                "MOD-0290 initial GSKU reference catalog",
                StringComparison.Ordinal))
        {
            return true;
        }

        return (catalog.Sets ?? []).Any(set =>
            string.Equals(set.SetCode?.Trim(), "pack-applicability", StringComparison.OrdinalIgnoreCase)
            || string.Equals(set.SetCode?.Trim(), "uom", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsVerifiedMarketCatalog(BusinessReferenceDataCatalogDocument catalog) =>
        (catalog.Sets ?? []).Any(set =>
            string.Equals(set.SetCode?.Trim(), VerifiedMarketCatalogContract.SetCode, StringComparison.OrdinalIgnoreCase));

    private static void ValidateRawMarketCatalog(BusinessReferenceDataCatalogDocument catalog)
    {
        if (catalog.Sets is not { Count: 1 }
            || !string.Equals(catalog.Sets[0].SetCode, VerifiedMarketCatalogContract.SetCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("market_catalog_identity_mismatch");
        }

        var activeValues = catalog.Sets[0].Values?.Where(value => value.IsActive).ToList() ?? [];
        if (activeValues.Count is 0
            || activeValues.Count > VerifiedMarketCatalogContract.MaximumActiveMarketCount
            || activeValues.Any(value =>
                !VerifiedMarketCatalogContract.IsCanonicalCode(value.ValueCode)
                || string.IsNullOrWhiteSpace(value.DisplayName)
                || !string.Equals(value.DisplayName, value.DisplayName.Trim(), StringComparison.Ordinal)
                || value.SortOrder < 0)
            || activeValues.Select(value => value.ValueCode).Distinct(StringComparer.Ordinal).Count() != activeValues.Count)
        {
            throw new InvalidOperationException("market_catalog_active_values_invalid");
        }
    }

    private static void ValidateLockedMarketCatalog(
        BusinessReferenceDataCatalogDocument catalog,
        IReadOnlyList<BusinessReferenceDataCatalogSetDocument> sets)
    {
        if (string.IsNullOrWhiteSpace(catalog.CatalogVersion)
            || sets.Count != 1
            || !string.Equals(sets[0].SetCode, VerifiedMarketCatalogContract.SetCode, StringComparison.Ordinal)
            || !string.Equals(sets[0].ScopeType, "global", StringComparison.Ordinal)
            || !string.Equals(sets[0].Status, "Active", StringComparison.Ordinal)
            || sets[0].AttributeDefinitions.Count != 0)
        {
            throw new InvalidOperationException("market_catalog_contract_mismatch");
        }
    }

    private async Task PreflightCatalogAsync(
        IReadOnlyList<BusinessReferenceDataCatalogSetDocument> sets,
        string catalogVersion,
        string catalogFingerprint,
        BusinessReferenceDataCatalogLoadSummary summary,
        bool verifiedMarket,
        CancellationToken ct)
    {
        foreach (var setDoc in sets)
        {
            var set = await _repository.GetSetByCodeAsync(setDoc.SetCode!, ct);
            if (set is null)
            {
                continue;
            }

            if (!string.Equals(set.Name, setDoc.SetName, StringComparison.Ordinal)
                || !string.Equals(set.ScopeType, setDoc.ScopeType, StringComparison.OrdinalIgnoreCase))
            {
                summary.BlockedConflicts.Add($"set_code={setDoc.SetCode}: locked set identity conflicts with existing data.");
                continue;
            }

            var versions = await _repository.GetVersionsBySetIdAsync(set.BusinessReferenceDataSetId, ct);
            if (verifiedMarket && HasMarketHistoryConflict(set, versions, setDoc, out var historyConflict))
            {
                summary.BlockedConflicts.Add($"set_code={setDoc.SetCode}: {historyConflict}");
                continue;
            }

            var idempotencyKey = BuildIdempotencyKey(catalogVersion, setDoc.SetCode!);
            var operation = await _repository.GetPublishOperationByIdempotencyKeyAsync(idempotencyKey, ct);
            if (operation is not null
                && (!string.Equals(operation.CatalogVersion, catalogVersion, StringComparison.Ordinal)
                    || !string.Equals(operation.CatalogFingerprint, catalogFingerprint, StringComparison.Ordinal)))
            {
                summary.BlockedConflicts.Add($"set_code={setDoc.SetCode}: catalog fingerprint conflict.");
                continue;
            }

            var matching = operation is null
                ? null
                : versions.SingleOrDefault(x => x.BusinessReferenceDataVersionId == operation.BusinessReferenceDataVersionId);
            if (operation is not null && matching is null)
            {
                summary.BlockedConflicts.Add($"set_code={setDoc.SetCode}: publish operation target is missing.");
                continue;
            }

            if (matching is not null && !VersionMatchesDocument(matching, setDoc))
            {
                summary.BlockedConflicts.Add($"set_code={setDoc.SetCode}: persisted catalog content conflicts with immutable artifact.");
                continue;
            }

            var published = versions.SingleOrDefault(x =>
                x.BusinessReferenceDataVersionId == set.PublishedVersionId && !x.IsDeleted);
            if (published is not null
                && !verifiedMarket
                && (operation is null
                    || operation.BusinessReferenceDataVersionId != published.BusinessReferenceDataVersionId
                    || !VersionMatchesDocument(published, setDoc)))
            {
                summary.BlockedConflicts.Add($"set_code={setDoc.SetCode}: published locked catalog differs from artifact.");
            }
        }
    }

    private static bool HasMarketHistoryConflict(
        BusinessReferenceDataSet set,
        IReadOnlyList<BusinessReferenceDataVersion> versions,
        BusinessReferenceDataCatalogSetDocument incoming,
        out string reason)
    {
        var historical = versions
            .Where(version => !version.IsDeleted)
            .SelectMany(version => version.Values)
            .ToList();
        foreach (var value in incoming.Values)
        {
            var priorDefinitions = historical
                .Where(prior => string.Equals(prior.ValueCode, value.ValueCode, StringComparison.Ordinal))
                .ToList();
            if (priorDefinitions.Any(prior =>
                    !string.Equals(prior.DisplayName, value.DisplayName, StringComparison.Ordinal)
                    || !string.Equals(prior.Description, value.Description, StringComparison.Ordinal)))
            {
                reason = $"value_code '{value.ValueCode}' cannot be reused for another meaning.";
                return true;
            }
        }

        var latest = versions.SingleOrDefault(version =>
            version.BusinessReferenceDataVersionId == set.PublishedVersionId && !version.IsDeleted);
        if (latest is not null)
        {
            foreach (var value in incoming.Values.Where(value => value.IsActive))
            {
                var latestValue = latest.Values.SingleOrDefault(prior =>
                    string.Equals(prior.ValueCode, value.ValueCode, StringComparison.Ordinal));
                if (historical.Any(prior => string.Equals(prior.ValueCode, value.ValueCode, StringComparison.Ordinal))
                    && (latestValue is null || latestValue.IsDeprecated))
                {
                    reason = $"retired value_code '{value.ValueCode}' cannot be reactivated.";
                    return true;
                }
            }
        }

        reason = string.Empty;
        return false;
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

    private static string BuildIdempotencyKey(string catalogVersion, string setCode, string? operationalNamespace = null)
    {
        var key = $"BusinessReferenceData-catalog-v{catalogVersion}:{setCode}".ToLowerInvariant();
        return string.IsNullOrWhiteSpace(operationalNamespace) ? key : $"{operationalNamespace.Trim()}:{key}";
    }

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

    private static void ValidateNoDuplicateJsonProperties(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        Inspect(document.RootElement);

        static void Inspect(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new InvalidOperationException($"catalog_duplicate_property:{property.Name}");
                    }

                    Inspect(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Inspect(item);
                }
            }
        }
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

        var attributeDefinitions = (raw.AttributeDefinitions ?? [])
            .Select(definition =>
            {
                var code = definition.AttributeCode?.Trim();
                var name = definition.DisplayName?.Trim();
                var dataType = definition.DataType?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dataType))
                {
                    throw new InvalidOperationException($"catalog_attribute_definition_shape_mismatch:{setCode}");
                }

                return new BusinessReferenceDataCatalogAttributeDefinitionDocument(code, name, dataType, definition.IsRequired);
            })
            .ToList();

        var duplicateDefinition = attributeDefinitions
            .GroupBy(x => x.AttributeCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1)?.Key;
        if (duplicateDefinition is not null)
        {
            throw new InvalidOperationException($"catalog_duplicate_attribute_definition:{setCode}:{duplicateDefinition}");
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

                var attributes = (value.Attributes ?? new Dictionary<string, string>())
                    .ToDictionary(x => x.Key.Trim(), x => x.Value.Trim(), StringComparer.Ordinal);
                if (attributes.Keys.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidOperationException($"catalog_attribute_shape_mismatch:{setCode}:{code}");
                }

                return new BusinessReferenceDataCatalogValueDocument(
                    code,
                    name,
                    string.IsNullOrWhiteSpace(value.Description) ? null : value.Description.Trim(),
                    value.IsActive,
                    value.SortOrder,
                    attributes);
            })
            .ToList();

        return new BusinessReferenceDataCatalogSetDocument(
            setCode,
            setName,
            scopeType,
            string.IsNullOrWhiteSpace(raw.Status) ? "Active" : raw.Status.Trim(),
            string.IsNullOrWhiteSpace(raw.Description) ? null : raw.Description.Trim(),
            attributeDefinitions,
            values);
    }

    private static void ValidateLockedGskuCatalog(
        BusinessReferenceDataCatalogDocument catalog,
        IReadOnlyList<BusinessReferenceDataCatalogSetDocument> sets)
    {
        var containsLockedSet = sets.Any(x =>
            string.Equals(x.SetCode, "pack-applicability", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.SetCode, "uom", StringComparison.OrdinalIgnoreCase));
        if (!containsLockedSet)
        {
            return;
        }

        if (!string.Equals(catalog.CatalogVersion?.Trim(), "1.0.0", StringComparison.Ordinal)
            || !string.Equals(catalog.Note?.Trim(), "MOD-0290 initial GSKU reference catalog", StringComparison.Ordinal)
            || sets.Count != 2)
        {
            throw new InvalidOperationException("gsku_catalog_identity_mismatch");
        }

        var pack = sets.SingleOrDefault(x => string.Equals(x.SetCode, "pack-applicability", StringComparison.Ordinal));
        var uom = sets.SingleOrDefault(x => string.Equals(x.SetCode, "uom", StringComparison.Ordinal));
        if (pack is null || uom is null
            || !SetHeaderMatches(pack, "Pack Applicability", "Applicability rules for product and SKU pack quantities.")
            || pack.AttributeDefinitions.Count != 0
            || pack.Values.Count != 1
            || !ValueMatches(pack.Values[0], "SCALAR_QUANTITY_APPLIES", "Scalar Quantity Applies", 10,
                "A positive scalar pack quantity applies to the SKU presentation.", new Dictionary<string, string>()))
        {
            throw new InvalidOperationException("gsku_pack_applicability_contract_mismatch");
        }

        var expectedDefinitions = new Dictionary<string, (string Name, string Type)>(StringComparer.Ordinal)
        {
            ["DimensionCode"] = ("Dimension Code", "string"),
            ["MaximumDecimalPrecision"] = ("Maximum Decimal Precision", "integer")
        };
        if (!SetHeaderMatches(uom, "Unit of Measure", "Units of measure permitted for the initial GSKU pack quantity.")
            || uom.AttributeDefinitions.Count != expectedDefinitions.Count
            || uom.AttributeDefinitions.Any(x => !x.IsRequired
                || !expectedDefinitions.TryGetValue(x.AttributeCode!, out var expected)
                || !string.Equals(x.DisplayName, expected.Name, StringComparison.Ordinal)
                || !string.Equals(x.DataType, expected.Type, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("gsku_uom_attribute_definition_mismatch");
        }

        var expectedValues = new[]
        {
            ("C62", "One", 10, "COUNT", "0"),
            ("GRM", "Gram", 20, "MASS", "3"),
            ("KGM", "Kilogram", 30, "MASS", "3"),
            ("MLT", "Millilitre", 40, "VOLUME", "3"),
            ("LTR", "Litre", 50, "VOLUME", "3")
        };
        if (uom.Values.Count != expectedValues.Length
            || expectedValues.Any(expected => !uom.Values.Any(value => ValueMatches(
                value,
                expected.Item1,
                expected.Item2,
                expected.Item3,
                null,
                new Dictionary<string, string>
                {
                    ["DimensionCode"] = expected.Item4,
                    ["MaximumDecimalPrecision"] = expected.Item5
                }))))
        {
            throw new InvalidOperationException("gsku_uom_value_contract_mismatch");
        }
    }

    private static bool SetHeaderMatches(BusinessReferenceDataCatalogSetDocument set, string name, string description)
        => string.Equals(set.SetName, name, StringComparison.Ordinal)
           && string.Equals(set.ScopeType, "global", StringComparison.Ordinal)
           && string.Equals(set.Status, "Active", StringComparison.Ordinal)
           && string.Equals(set.Description, description, StringComparison.Ordinal);

    private static bool ValueMatches(
        BusinessReferenceDataCatalogValueDocument value,
        string code,
        string name,
        int sortOrder,
        string? description,
        IReadOnlyDictionary<string, string> attributes)
        => string.Equals(value.ValueCode, code, StringComparison.Ordinal)
           && string.Equals(value.DisplayName, name, StringComparison.Ordinal)
           && string.Equals(value.Description, description, StringComparison.Ordinal)
           && value.IsActive
           && value.SortOrder == sortOrder
           && DictionaryEquals(value.Attributes, attributes);

    private static bool VersionMatchesDocument(
        BusinessReferenceDataVersion version,
        BusinessReferenceDataCatalogSetDocument document)
    {
        if (version.AttributeDefinitions.Count != document.AttributeDefinitions.Count
            || version.AttributeDefinitions.Any(definition => !document.AttributeDefinitions.Any(expected =>
                string.Equals(definition.AttributeCode, expected.AttributeCode, StringComparison.Ordinal)
                && string.Equals(definition.DisplayName, expected.DisplayName, StringComparison.Ordinal)
                && string.Equals(definition.DataType, expected.DataType, StringComparison.Ordinal)
                && definition.IsRequired == expected.IsRequired))
            || version.Values.Count != document.Values.Count)
        {
            return false;
        }

        return version.Values.All(value => document.Values.Any(expected =>
            string.Equals(value.ValueCode, expected.ValueCode, StringComparison.Ordinal)
            && string.Equals(value.DisplayName, expected.DisplayName, StringComparison.Ordinal)
            && string.Equals(value.Description, expected.Description, StringComparison.Ordinal)
            && value.IsActive == expected.IsActive
            && value.SortOrder == expected.SortOrder
            && DictionaryEquals(value.Attributes, expected.Attributes)));
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        left ??= new Dictionary<string, string>();
        right ??= new Dictionary<string, string>();
        return left.Count == right.Count
               && left.All(x => right.TryGetValue(x.Key, out var value)
                                && string.Equals(x.Value, value, StringComparison.Ordinal));
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
    [property: JsonPropertyName("attribute_definitions")] IReadOnlyList<BusinessReferenceDataCatalogAttributeDefinitionDocument> AttributeDefinitions,
    [property: JsonPropertyName("values")] IReadOnlyList<BusinessReferenceDataCatalogValueDocument> Values)
{
    public BusinessReferenceDataCatalogSetDocument() : this(null, null, null, null, null, [], []) { }
}

public sealed record BusinessReferenceDataCatalogAttributeDefinitionDocument(
    [property: JsonPropertyName("attribute_code")] string? AttributeCode,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("data_type")] string? DataType,
    [property: JsonPropertyName("is_required")] bool IsRequired)
{
    public BusinessReferenceDataCatalogAttributeDefinitionDocument() : this(null, null, null, false) { }
}

public sealed record BusinessReferenceDataCatalogValueDocument(
    [property: JsonPropertyName("value_code")] string? ValueCode,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string> Attributes)
{
    public BusinessReferenceDataCatalogValueDocument() : this(null, null, null, true, 0, new Dictionary<string, string>()) { }
}

public sealed class BusinessReferenceDataCatalogLoadSummary
{
    public string FilePath { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public string CatalogFingerprint { get; set; } = string.Empty;
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
