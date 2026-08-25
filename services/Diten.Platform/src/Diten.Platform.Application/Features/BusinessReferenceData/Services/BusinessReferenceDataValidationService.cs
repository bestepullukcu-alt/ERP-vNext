using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class BusinessReferenceDataValidationService : IBusinessReferenceDataValidationService
{
    private static readonly string[] RuleIds =
    [
        "RDV-001", "RDV-002", "RDV-003", "RDV-004", "RDV-005",
        "RDV-006", "RDV-007", "RDV-008", "RDV-009", "RDV-010",
        "RDV-011", "RDV-012", "RDV-013", "RDV-014", "RDV-015",
        "RDV-016", "RDV-017", "RDV-018", "RDV-019", "RDV-020",
        "RDV-021", "RDV-022", "RDV-023", "RDV-024", "RDV-025"
    ];

    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ILogger<BusinessReferenceDataValidationService> _logger;

    public BusinessReferenceDataValidationService(
        IBusinessReferenceDataStewardshipRepository repository,
        ILogger<BusinessReferenceDataValidationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<BusinessReferenceDataValidationRunModel> ValidateDraftVersionAsync(Guid versionId, string? correlationId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var version = await _repository.GetVersionByIdAsync(versionId, ct);
        var results = new List<BusinessReferenceDataValidationResult>();

        foreach (var ruleId in RuleIds)
        {
            switch (ruleId)
            {
                case "RDV-001":
                    results.Add(MakeResult(versionId, ruleId, version is null
                        ? (BusinessReferenceDataValidationSeverity.Error, true, "Version not found.")
                        : (BusinessReferenceDataValidationSeverity.Info, false, "Version exists."), false, null, correlationId, now));
                    break;
                case "RDV-002":
                    results.Add(MakeResult(versionId, ruleId, version is null || version.Status == BusinessReferenceDataVersionStatus.Draft
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Draft-only edit enforcement satisfied.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, "Version is not in draft status."), false, null, correlationId, now));
                    break;
                case "RDV-003":
                    results.Add(MakeResult(versionId, ruleId, version is null || !HasInvalidWindow(version)
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Publish window is valid.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, "Publish window end must be greater than start."), false, null, correlationId, now));
                    break;
                case "RDV-004":
                    if (version is null)
                    {
                        results.Add(MakeResult(versionId, ruleId, (BusinessReferenceDataValidationSeverity.Info, false, "Skipped because version is missing."), false, null, correlationId, now));
                        break;
                    }

                    var overlapping = await HasOverlappingPublishedWindowAsync(version, ct);
                    results.Add(MakeResult(versionId, ruleId, overlapping
                        ? (BusinessReferenceDataValidationSeverity.Error, true, "Publish window overlaps an already published version.")
                        : (BusinessReferenceDataValidationSeverity.Info, false, "No overlapping publish windows detected."), false, null, correlationId, now));
                    break;
                case "RDV-005":
                    results.Add(MakeResult(versionId, ruleId, version is null || !version.RequiresEvidence || version.EvidenceAttached
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Evidence requirement satisfied.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, "Required evidence is missing."), false, null, correlationId, now));
                    break;
                case "RDV-006":
                    results.Add(MakeResult(versionId, ruleId, version is null || !version.RequiresApproval || version.ApprovedAt.HasValue
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Approval requirement satisfied.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, "Required approval is missing."), false, null, correlationId, now));
                    break;
                case "RDV-007":
                    results.Add(MakeResult(versionId, ruleId, version is null || !string.IsNullOrWhiteSpace(version.ConcurrencyToken)
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Concurrency token present.")
                        : (BusinessReferenceDataValidationSeverity.Warning, false, "Concurrency token is missing."), false, null, correlationId, now));
                    break;
                case "RDV-008":
                    if (version is null)
                    {
                        results.Add(MakeResult(versionId, ruleId, (BusinessReferenceDataValidationSeverity.Info, false, "Skipped because version is missing."), false, null, correlationId, now));
                        break;
                    }

                    var duplicateCodes = FindDuplicateValueCodes(version);
                    results.Add(MakeResult(versionId, ruleId, duplicateCodes.Count == 0
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Value codes are unique.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, $"Duplicate value codes detected: {string.Join(", ", duplicateCodes)}."), false, null, correlationId, now));
                    break;
                case "RDV-009":
                    if (version is null)
                    {
                        results.Add(MakeResult(versionId, ruleId, (BusinessReferenceDataValidationSeverity.Info, false, "Skipped because version is missing."), false, null, correlationId, now));
                        break;
                    }

                    var requiredFieldIssues = ValidateRequiredValueFields(version);
                    results.Add(MakeResult(versionId, ruleId, requiredFieldIssues.Count == 0
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Required value fields are present.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, $"Required value field issues: {string.Join("; ", requiredFieldIssues)}."), false, null, correlationId, now));
                    break;
                case "RDV-010":
                    if (version is null)
                    {
                        results.Add(MakeResult(versionId, ruleId, (BusinessReferenceDataValidationSeverity.Info, false, "Skipped because version is missing."), false, null, correlationId, now));
                        break;
                    }

                    var invalidSortOrders = FindInvalidSortOrders(version);
                    results.Add(MakeResult(versionId, ruleId, invalidSortOrders.Count == 0
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Sort orders are valid.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, $"Invalid sort order found for value codes: {string.Join(", ", invalidSortOrders)}."), false, null, correlationId, now));
                    break;
                case "RDV-011":
                    if (version is null)
                    {
                        results.Add(MakeResult(versionId, ruleId, (BusinessReferenceDataValidationSeverity.Info, false, "Skipped because version is missing."), false, null, correlationId, now));
                        break;
                    }

                    var attributeIssues = ValidateAttributeContract(version);
                    results.Add(MakeResult(versionId, ruleId, attributeIssues.Count == 0
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Attribute definitions and values are valid.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, $"Attribute contract issues: {string.Join("; ", attributeIssues)}."), false, null, correlationId, now));
                    break;
                case "RDV-012":
                    if (version is null)
                    {
                        results.Add(MakeResult(versionId, ruleId, (BusinessReferenceDataValidationSeverity.Info, false, "Skipped because version is missing."), false, null, correlationId, now));
                        break;
                    }

                    var owningSet = await _repository.GetSetByIdAsync(version.BusinessReferenceDataSetId, ct);
                    var lockedIssues = ValidateLockedGskuContract(owningSet?.SetCode, version);
                    results.Add(MakeResult(versionId, ruleId, lockedIssues.Count == 0
                        ? (BusinessReferenceDataValidationSeverity.Info, false, "Locked GSKU catalog contract is valid.")
                        : (BusinessReferenceDataValidationSeverity.Error, true, $"Locked GSKU catalog issues: {string.Join("; ", lockedIssues)}."), false, null, correlationId, now));
                    break;
                default:
                    results.Add(MakeStubResult(versionId, ruleId, correlationId, now, "Rule implementation deferred to later batches."));
                    break;
            }
        }

        await _repository.ReplaceValidationResultsAsync(versionId, results, ct);
        var reloaded = await _repository.GetValidationResultsByVersionAsync(versionId, ct);
        var blockers = PublishBlockerEvaluator.Evaluate(reloaded);
        var blockingErrorCount = reloaded.Count(x => x.Severity == BusinessReferenceDataValidationSeverity.Error && x.IsBlocking);
        var warningCount = reloaded.Count(x => x.Severity == BusinessReferenceDataValidationSeverity.Warning);
        var infoCount = reloaded.Count(x => x.Severity == BusinessReferenceDataValidationSeverity.Info);

        return new BusinessReferenceDataValidationRunModel(
            versionId,
            blockingErrorCount,
            warningCount,
            infoCount,
            blockers.Count > 0,
            blockers,
            reloaded
                .OrderBy(x => x.RuleId, StringComparer.Ordinal)
                .Select(x => new BusinessReferenceDataValidationResultModel(
                    x.RuleId,
                    x.Severity.ToString(),
                    x.IsBlocking,
                    x.Message,
                    x.IsStubbed,
                    x.StubReason))
                .ToList());
    }

    private async Task<bool> HasOverlappingPublishedWindowAsync(BusinessReferenceDataVersion version, CancellationToken ct)
    {
        var publishedVersions = await _repository.GetPublishedVersionsBySetAsync(version.BusinessReferenceDataSetId, version.BusinessReferenceDataVersionId, ct);
        return publishedVersions.Any(existing =>
            Overlaps(existing.PublishWindowStart, existing.PublishWindowEnd, version.PublishWindowStart, version.PublishWindowEnd));
    }

    private static bool HasInvalidWindow(BusinessReferenceDataVersion version)
    {
        return version.PublishWindowStart.HasValue
               && version.PublishWindowEnd.HasValue
               && version.PublishWindowEnd <= version.PublishWindowStart;
    }

    private static bool Overlaps(DateTimeOffset? aStart, DateTimeOffset? aEnd, DateTimeOffset? bStart, DateTimeOffset? bEnd)
    {
        if (!aStart.HasValue || !bStart.HasValue)
        {
            return false;
        }

        var leftEnd = aEnd ?? DateTimeOffset.MaxValue;
        var rightEnd = bEnd ?? DateTimeOffset.MaxValue;
        return aStart <= rightEnd && bStart <= leftEnd;
    }

    private static List<string> FindDuplicateValueCodes(BusinessReferenceDataVersion version)
    {
        return version.Values
            .Select(x => NormalizeCode(x.ValueCode))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ValidateRequiredValueFields(BusinessReferenceDataVersion version)
    {
        var issues = new List<string>();
        if (version.Values.Count == 0)
        {
            issues.Add("At least one value is required");
            return issues;
        }

        foreach (var value in version.Values)
        {
            var valueCode = NormalizeCode(value.ValueCode);
            if (string.IsNullOrWhiteSpace(valueCode))
            {
                issues.Add("value.code is required");
            }

            if (string.IsNullOrWhiteSpace(value.DisplayName))
            {
                var codeTag = string.IsNullOrWhiteSpace(valueCode) ? "<missing-code>" : valueCode;
                issues.Add($"value.label is required for {codeTag}");
            }
        }

        return issues;
    }

    private static List<string> FindInvalidSortOrders(BusinessReferenceDataVersion version)
    {
        return version.Values
            .Where(x => x.SortOrder < 0)
            .Select(x => NormalizeCode(x.ValueCode) ?? "<missing-code>")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ValidateAttributeContract(BusinessReferenceDataVersion version)
    {
        var issues = new List<string>();
        var duplicateDefinitions = version.AttributeDefinitions
            .GroupBy(x => NormalizeCode(x.AttributeCode), StringComparer.OrdinalIgnoreCase)
            .Where(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1)
            .Select(x => x.Key ?? "<missing-code>");
        issues.AddRange(duplicateDefinitions.Select(x => $"duplicate attribute definition {x}"));

        var definitions = version.AttributeDefinitions
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeCode))
            .GroupBy(x => x.AttributeCode.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        foreach (var definition in definitions.Values)
        {
            if (!string.Equals(definition.DataType, "string", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(definition.DataType, "integer", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"unsupported data type {definition.AttributeCode}:{definition.DataType}");
            }
        }

        foreach (var value in version.Values)
        {
            var attributes = value.Attributes ?? new Dictionary<string, string>();
            foreach (var key in attributes.Keys.Where(key => !definitions.ContainsKey(key)))
            {
                issues.Add($"unknown attribute {value.ValueCode}:{key}");
            }

            foreach (var definition in definitions.Values.Where(x => x.IsRequired))
            {
                if (!attributes.TryGetValue(definition.AttributeCode, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    issues.Add($"missing required attribute {value.ValueCode}:{definition.AttributeCode}");
                }
            }

            foreach (var pair in attributes)
            {
                if (!definitions.TryGetValue(pair.Key, out var definition)
                    || !string.Equals(definition.DataType, "integer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!int.TryParse(pair.Value, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                    || parsed < 0
                    || !string.Equals(parsed.ToString(System.Globalization.CultureInfo.InvariantCulture), pair.Value, StringComparison.Ordinal))
                {
                    issues.Add($"invalid canonical non-negative integer {value.ValueCode}:{pair.Key}");
                }
            }
        }

        return issues;
    }

    private static List<string> ValidateLockedGskuContract(string? setCode, BusinessReferenceDataVersion version)
    {
        if (string.Equals(setCode, "pack-applicability", StringComparison.Ordinal))
        {
            return version.AttributeDefinitions.Count == 0
                   && version.Values.Count == 1
                   && LockedValueMatches(version.Values[0], "SCALAR_QUANTITY_APPLIES", "Scalar Quantity Applies", null, null)
                ? []
                : ["pack-applicability must contain only SCALAR_QUANTITY_APPLIES"];
        }

        if (!string.Equals(setCode, "uom", StringComparison.Ordinal))
        {
            return [];
        }

        var expected = new Dictionary<string, (string Name, string Dimension, string Precision)>(StringComparer.Ordinal)
        {
            ["C62"] = ("One", "COUNT", "0"),
            ["GRM"] = ("Gram", "MASS", "3"),
            ["KGM"] = ("Kilogram", "MASS", "3"),
            ["MLT"] = ("Millilitre", "VOLUME", "3"),
            ["LTR"] = ("Litre", "VOLUME", "3")
        };
        var definitionsValid = version.AttributeDefinitions.Count == 2
                               && version.AttributeDefinitions.Any(x => x.AttributeCode == "DimensionCode"
                                                                        && x.DataType == "string" && x.IsRequired)
                               && version.AttributeDefinitions.Any(x => x.AttributeCode == "MaximumDecimalPrecision"
                                                                        && x.DataType == "integer" && x.IsRequired);
        if (!definitionsValid || version.Values.Count != expected.Count
            || version.Values.Any(value => !expected.TryGetValue(value.ValueCode, out var contract)
                                           || !LockedValueMatches(value, value.ValueCode, contract.Name, contract.Dimension, contract.Precision)))
        {
            return ["uom values, dimensions or precision differ from the locked contract"];
        }

        return [];
    }

    private static bool LockedValueMatches(
        BusinessReferenceDataValue value,
        string code,
        string name,
        string? dimension,
        string? precision)
    {
        var attributes = value.Attributes ?? new Dictionary<string, string>();
        if (!string.Equals(value.ValueCode, code, StringComparison.Ordinal)
            || !string.Equals(value.DisplayName, name, StringComparison.Ordinal)
            || value.IsDeprecated)
        {
            return false;
        }

        return dimension is null
            ? attributes.Count == 0
            : attributes.Count == 2
              && attributes.TryGetValue("DimensionCode", out var actualDimension)
              && string.Equals(actualDimension, dimension, StringComparison.Ordinal)
              && attributes.TryGetValue("MaximumDecimalPrecision", out var actualPrecision)
              && string.Equals(actualPrecision, precision, StringComparison.Ordinal);
    }

    private static string? NormalizeCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
    }

    private static BusinessReferenceDataValidationResult MakeResult(
        Guid versionId,
        string ruleId,
        (BusinessReferenceDataValidationSeverity Severity, bool Blocking, string Message) outcome,
        bool stubbed,
        string? stubReason,
        string? correlationId,
        DateTimeOffset executedAt)
    {
        return new BusinessReferenceDataValidationResult
        {
            TenantId = Guid.Empty, // repository normalizes tenant binding
            BusinessReferenceDataVersionId = versionId,
            RuleId = ruleId,
            Severity = outcome.Severity,
            IsBlocking = outcome.Blocking,
            Message = outcome.Message,
            IsStubbed = stubbed,
            StubReason = stubReason,
            CorrelationId = correlationId,
            ExecutedAt = executedAt,
            CreatedBy = "system"
        };
    }

    private BusinessReferenceDataValidationResult MakeStubResult(
        Guid versionId,
        string ruleId,
        string? correlationId,
        DateTimeOffset executedAt,
        string reason)
    {
        _logger.LogWarning("BusinessReferenceData validation rule {RuleId} executed as stub. Reason: {Reason}", ruleId, reason);
        return MakeResult(
            versionId,
            ruleId,
            (BusinessReferenceDataValidationSeverity.Info, false, "Stubbed rule. No blocking decision produced."),
            true,
            reason,
            correlationId,
            executedAt);
    }
}

public static class PublishBlockerEvaluator
{
    public static IReadOnlyList<string> Evaluate(IReadOnlyList<BusinessReferenceDataValidationResult> results)
    {
        var blockers = new List<string>();
        foreach (var result in results.Where(x => x.IsBlocking && x.Severity == BusinessReferenceDataValidationSeverity.Error))
        {
            blockers.Add(result.RuleId);
        }

        return blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }
}
