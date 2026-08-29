using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Features.Territory;

/// <inheritdoc />
public sealed class TerritoryReferenceValidator : ITerritoryReferenceValidator
{
    private readonly IReferenceDataValidator _validator;
    private readonly IReferenceMetadataReader _metadata;
    private readonly IReferenceDataCatalogReader _catalog;

    public TerritoryReferenceValidator(
        IReferenceDataValidator validator,
        IReferenceMetadataReader metadata,
        IReferenceDataCatalogReader catalog)
    {
        _validator = validator;
        _metadata = metadata;
        _catalog = catalog;
    }

    public async Task<ReferenceValidationStatus> ValidateValueAsync(string setCode, string value, CancellationToken cancellationToken)
        => (await _validator.ValidateAsync(setCode, value, cancellationToken)).Status;

    public async Task<IReadOnlyDictionary<string, string>?> GetValueMetadataAsync(
        string setCode, string value, CancellationToken cancellationToken)
        => await _metadata.GetValueAttributesAsync(setCode, value, cancellationToken);

    public async Task<LevelRankResult> ResolveLevelRankAsync(string levelCode, CancellationToken cancellationToken)
    {
        var status = await ValidateValueAsync(TerritoryReferenceSets.TerritoryLevel, levelCode, cancellationToken);
        switch (status)
        {
            case ReferenceValidationStatus.SetMissing:
                return LevelRankResult.Fail(TerritoryReferenceIssue.SetMissing);
            case ReferenceValidationStatus.InvalidValue:
                return LevelRankResult.Fail(TerritoryReferenceIssue.InvalidValue);
        }

        // Value is valid — the rank metadata MUST be present and parseable, else fail closed (never a default rank).
        var attributes = await _metadata.GetValueAttributesAsync(TerritoryReferenceSets.TerritoryLevel, levelCode, cancellationToken);
        if (attributes is null || !attributes.ContainsKey(TerritoryReferenceSets.RankMetadataKey))
        {
            return LevelRankResult.Fail(TerritoryReferenceIssue.MetadataMissing);
        }

        if (!ReferenceMetadata.TryGetInt(attributes, TerritoryReferenceSets.RankMetadataKey, out var rank))
        {
            return LevelRankResult.Fail(TerritoryReferenceIssue.MetadataInvalid);
        }

        return LevelRankResult.Success(rank);
    }

    public async Task<IReadOnlyList<TerritoryReferenceSetReadiness>> GetReadinessAsync(CancellationToken cancellationToken)
    {
        var readiness = new List<TerritoryReferenceSetReadiness>(TerritoryReferenceSets.Required.Count);

        foreach (var descriptor in TerritoryReferenceSets.Required)
        {
            var snapshot = await _catalog.GetPublishedValuesAsync(descriptor.SetCode, cancellationToken);
            var activeValues = snapshot.Values.Where(v => v.IsActive && !v.IsDeprecated).ToList();

            var missingMetadata = new List<string>();
            if (descriptor.RequiredMetadataKeys.Count != 0)
            {
                foreach (var key in descriptor.RequiredMetadataKeys)
                {
                    // "metadata ready" only when EVERY active value carries the key (partial coverage is not ready).
                    var coveredByAll = activeValues.Count != 0
                        && activeValues.All(v => v.Attributes is { } attrs
                                                 && attrs.TryGetValue(key, out var raw)
                                                 && !string.IsNullOrWhiteSpace(raw));
                    if (!coveredByAll)
                    {
                        missingMetadata.Add(key);
                    }
                }
            }

            var metadataReady = missingMetadata.Count == 0;
            var ready = snapshot.IsPublished && activeValues.Count > 0 && metadataReady;

            readiness.Add(new TerritoryReferenceSetReadiness(
                descriptor.SetCode,
                Required: true,
                Ready: ready,
                ExpectedValueCount: descriptor.ExpectedValueCount,
                ActualValueCount: activeValues.Count,
                MetadataReady: metadataReady,
                MissingMetadata: missingMetadata));
        }

        return readiness;
    }
}
