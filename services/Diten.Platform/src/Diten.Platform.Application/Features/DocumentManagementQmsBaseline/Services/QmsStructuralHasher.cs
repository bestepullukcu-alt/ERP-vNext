using System.Security.Cryptography;
using System.Text;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Deterministic structural hashing for QMS definitions. Pure functions over canonical string projections, so the
/// same input always produces the same hash regardless of evaluation order or environment.
/// </summary>
public static class QmsStructuralHasher
{
    // ASCII unit separator (U+001F): a stable, content-safe field delimiter for canonical projections.
    public const char FieldSeparator = '';

    /// <summary>Per-definition deterministic hash over the structural fields (ordering-independent of siblings).</summary>
    public static string HashDefinition(QmsCollectionDefinitionDraft d)
    {
        var canonical = string.Join(
            FieldSeparator,
            d.CanonicalId,
            d.ParentCanonicalId ?? string.Empty,
            QmsFolderPathNormalizer.CaseInsensitiveKey(d.FullPath),
            d.Name,
            d.PurposeScope ?? string.Empty,
            d.RequiredByScope ?? string.Empty,
            d.AllowsManualChildren ? "1" : "0",
            d.TemplatesAllowed ? "1" : "0",
            d.AllowedDocClass ?? string.Empty,
            d.DefaultClassificationLevel ?? string.Empty,
            d.DefaultRetentionHint ?? string.Empty,
            d.IsMandatory ? "1" : "0",
            d.IsAutoProvisioned ? "1" : "0",
            d.IsProtected ? "1" : "0",
            d.DisplayOrder.ToString());

        return Sha256Hex(canonical);
    }

    /// <summary>Hash over the structural-controls slice of an ordered definition set.</summary>
    public static string HashStructuralControls(IReadOnlyList<(string CanonicalId, string ControlLine)> orderedControls)
    {
        var builder = new StringBuilder();
        foreach (var (canonicalId, controlLine) in orderedControls)
        {
            builder.Append(canonicalId).Append(FieldSeparator).Append(controlLine).Append('\n');
        }

        return Sha256Hex(builder.ToString());
    }

    /// <summary>Top-level snapshot hash over the structural-controls hash and the ordered per-definition hashes.</summary>
    public static string ComputeSnapshotHash(string structuralControlsHash, IReadOnlyList<string> orderedDefinitionHashes) =>
        Sha256Hex(structuralControlsHash + "\n" + string.Join('\n', orderedDefinitionHashes));

    public static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
