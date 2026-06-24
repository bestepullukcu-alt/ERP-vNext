using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline;

/// <summary>Shared projection + failure-classification helpers for the FU02 handlers.</summary>
public static class QmsBaselineMapping
{
    /// <summary>Classifies a validation summary into a controlled HTTP failure, or null when the plan is valid.</summary>
    public static (int Status, string ReasonCode, IReadOnlyList<string> Errors)? ClassifyFailure(QmsBaselineImportSummary summary)
    {
        if (summary.DuplicatePathConflicts.Count > 0)
        {
            return (409, QmsBaselineReasonCodes.Conflict, summary.DuplicatePathConflicts);
        }

        if (summary.Errors.Count > 0 || summary.InvalidHierarchyFindings.Count > 0)
        {
            return (400, QmsBaselineReasonCodes.ValidationFailed, [.. summary.Errors, .. summary.InvalidHierarchyFindings]);
        }

        return null;
    }

    public static QmsBaselineSummaryModel ToSummaryModel(BaselineRelease baseline, int definitionCount) => new(
        baseline.Id,
        baseline.BaselineReleaseId,
        baseline.BaselineVersion,
        baseline.Status.ToString().ToUpperInvariant(),
        baseline.SnapshotHash,
        baseline.ManifestId,
        definitionCount,
        baseline.CreatedAt,
        baseline.PublishedAt,
        baseline.ChangeSummary,
        baseline.EffectiveDate,
        baseline.Version);

    public static QmsCollectionDefinitionModel ToDefinitionModel(CollectionDefinition d) => new(
        d.CanonicalId,
        d.ParentCanonicalId,
        d.Name,
        d.PurposeScope,
        d.RequiredByScope,
        d.AllowsManualChildren,
        d.TemplatesAllowed,
        d.AllowedDocClass,
        d.DefaultClassificationLevel,
        d.DefaultRetentionHint,
        d.IsMandatory,
        d.IsAutoProvisioned,
        d.IsProtected,
        d.PathSegment,
        d.FullPath,
        d.DisplayOrder,
        d.Status.ToString().ToUpperInvariant(),
        d.DefinitionHash,
        d.Version);
}
