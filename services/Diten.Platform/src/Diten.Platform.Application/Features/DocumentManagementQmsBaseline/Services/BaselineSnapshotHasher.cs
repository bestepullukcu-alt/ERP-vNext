using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Produces the deterministic, reproducible manifest payload for a published baseline. Identical persisted
/// definitions always yield identical <see cref="QmsManifestComputation.SnapshotHash"/> and per-definition hashes.
/// </summary>
public sealed class BaselineSnapshotHasher
{
    public QmsManifestComputation Compute(IReadOnlyList<CollectionDefinition> definitions)
    {
        var ordered = definitions
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => QmsFolderPathNormalizer.CaseInsensitiveKey(d.FullPath), StringComparer.Ordinal)
            .ToList();

        var definitionIds = ordered.Select(d => d.CanonicalId).ToList();
        var definitionHashes = ordered.Select(d => d.DefinitionHash).ToList();

        var controls = ordered
            .Select(d => (
                d.CanonicalId,
                ControlLine: string.Join(
                    QmsStructuralHasher.FieldSeparator,
                    d.RequiredByScope ?? string.Empty,
                    d.IsMandatory ? "1" : "0",
                    d.AllowedDocClass ?? string.Empty,
                    d.DefaultClassificationLevel ?? string.Empty,
                    d.DefaultRetentionHint ?? string.Empty,
                    d.AllowsManualChildren ? "1" : "0",
                    d.TemplatesAllowed ? "1" : "0",
                    d.IsProtected ? "1" : "0")))
            .ToList();

        var structuralControlsHash = QmsStructuralHasher.HashStructuralControls(controls);
        var snapshotHash = QmsStructuralHasher.ComputeSnapshotHash(structuralControlsHash, definitionHashes);

        return new QmsManifestComputation(definitionIds, definitionHashes, structuralControlsHash, snapshotHash);
    }
}

public sealed record QmsManifestComputation(
    IReadOnlyList<string> DefinitionIds,
    IReadOnlyList<string> DefinitionHashes,
    string StructuralControlsHash,
    string SnapshotHash);
