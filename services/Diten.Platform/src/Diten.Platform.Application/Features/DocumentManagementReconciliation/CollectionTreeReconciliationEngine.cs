using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation;

/// <summary>
/// MOD-0028-FU09 — pure, deterministic read-back comparison. No I/O, no tenant, no persistence: the same expected +
/// actual sets always yield the same deviations. Matching prefers the stable register <c>folder_id</c>; when absent it
/// falls back to the (case-insensitive) full path. It only detects and reports differences — it never mutates,
/// creates, moves, renames or deletes anything.
/// </summary>
public static class CollectionTreeReconciliationEngine
{
    public static IReadOnlyList<DeviationDetail> Compare(
        IReadOnlyList<ExpectedNode> expected,
        IReadOnlyList<ReadBackNode> actual)
    {
        var deviations = new List<DeviationDetail>();

        // Structural checks over the ACTUAL tree (duplicates / siblings / orphans).
        DetectDuplicateFullPaths(actual, deviations);
        DetectDuplicateSiblings(actual, deviations);
        DetectOrphans(actual, deviations);

        var actualByFolderId = actual
            .Where(a => !string.IsNullOrWhiteSpace(a.RegisterFolderId))
            .GroupBy(a => a.RegisterFolderId!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var actualByPath = actual
            .GroupBy(a => PathKey(a.FullPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var matchedActual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in expected)
        {
            var match = MatchActual(e, actualByFolderId, actualByPath);
            if (match is null)
            {
                deviations.Add(new DeviationDetail(
                    CollectionDeviationType.MissingFolder, DeviationSeverity.Critical,
                    e.RegisterFolderId, e.CollectionInstanceId, e.FullPath, null,
                    $"Expected folder '{e.FullPath}' was not found in the live tree.",
                    "Provision the missing folder from the register, then re-run read-back."));
                continue;
            }

            matchedActual.Add(ActualKey(match));

            // Rename: matched by stable id but the name/path differs.
            if (!string.IsNullOrWhiteSpace(e.RegisterFolderId)
                && !PathKey(e.FullPath).Equals(PathKey(match.FullPath), StringComparison.OrdinalIgnoreCase))
            {
                deviations.Add(new DeviationDetail(
                    CollectionDeviationType.RenameMismatch, DeviationSeverity.Major,
                    e.RegisterFolderId, match.CollectionInstanceId, e.FullPath, match.FullPath,
                    $"Folder '{e.RegisterFolderId}' is named '{match.Name}' but the register expects '{e.Name}'.",
                    "Rename in the register first, regenerate, then reconcile — do not rename the live folder ad hoc."));
            }

            // Move: matched by stable id but the parent differs.
            if (!PathKey(e.ParentFullPath).Equals(PathKey(match.ParentFullPath), StringComparison.OrdinalIgnoreCase))
            {
                deviations.Add(new DeviationDetail(
                    CollectionDeviationType.MoveMismatch, DeviationSeverity.Major,
                    e.RegisterFolderId, match.CollectionInstanceId, e.FullPath, match.FullPath,
                    $"Folder '{e.FullPath}' sits under '{match.ParentFullPath}' but the register expects '{e.ParentFullPath}'.",
                    "Reconcile the move in the register; do not move the live folder ad hoc."));
            }

            // Metadata: governance mismatch (minimum: AccessProfile / FolderType / RetentionClass).
            DetectMetadataMismatch(e, match, deviations);
        }

        // Extra: an actual node no expected node matched.
        foreach (var a in actual)
        {
            if (matchedActual.Contains(ActualKey(a)))
            {
                continue;
            }

            var matchedByExpected = expected.Any(e =>
                (!string.IsNullOrWhiteSpace(e.RegisterFolderId) && !string.IsNullOrWhiteSpace(a.RegisterFolderId)
                    && e.RegisterFolderId!.Trim().Equals(a.RegisterFolderId!.Trim(), StringComparison.OrdinalIgnoreCase))
                || PathKey(e.FullPath).Equals(PathKey(a.FullPath), StringComparison.OrdinalIgnoreCase));
            if (matchedByExpected)
            {
                continue;
            }

            deviations.Add(new DeviationDetail(
                CollectionDeviationType.ExtraFolder, DeviationSeverity.Major,
                a.RegisterFolderId, a.CollectionInstanceId, a.FullPath, a.FullPath,
                $"Live folder '{a.FullPath}' has no register row (controlled repository must not contain unregistered folders).",
                "Add a register row for it, or remove it through a governed change — do not leave it unregistered."));
        }

        return deviations;
    }

    public static ReconciliationSummary Summarize(
        IReadOnlyList<ExpectedNode> expected,
        IReadOnlyList<ReadBackNode> actual,
        IReadOnlyList<DeviationDetail> deviations)
    {
        int Count(CollectionDeviationType t) => deviations.Count(d => d.DeviationType == t);
        var missing = Count(CollectionDeviationType.MissingFolder);
        var extra = Count(CollectionDeviationType.ExtraFolder);
        var renamed = Count(CollectionDeviationType.RenameMismatch);
        var moved = Count(CollectionDeviationType.MoveMismatch);
        var metadata = Count(CollectionDeviationType.MetadataMismatch);
        var blocking = deviations.Count(d => d.Severity is DeviationSeverity.Major or DeviationSeverity.Critical);

        return new ReconciliationSummary(
            ExpectedCount: expected.Count,
            ActualCount: actual.Count,
            MatchedCount: Math.Max(0, expected.Count - missing),
            MissingCount: missing,
            ExtraCount: extra,
            RenamedCount: renamed,
            MovedCount: moved,
            MetadataMismatchCount: metadata,
            DeviationCount: deviations.Count,
            BlockingDeviationCount: blocking);
    }

    private static ReadBackNode? MatchActual(
        ExpectedNode e,
        IReadOnlyDictionary<string, ReadBackNode> byFolderId,
        IReadOnlyDictionary<string, ReadBackNode> byPath)
    {
        if (!string.IsNullOrWhiteSpace(e.RegisterFolderId) && byFolderId.TryGetValue(e.RegisterFolderId.Trim(), out var byId))
        {
            return byId;
        }

        return byPath.TryGetValue(PathKey(e.FullPath), out var byP) ? byP : null;
    }

    private static void DetectMetadataMismatch(ExpectedNode e, ReadBackNode match, List<DeviationDetail> deviations)
    {
        var mismatches = new List<string>();
        CheckMeta(match, "AccessProfile", e.AccessProfile, mismatches);
        CheckMeta(match, "FolderType", e.FolderType, mismatches);
        CheckMeta(match, "RetentionClass", e.RetentionClass, mismatches);

        if (mismatches.Count > 0)
        {
            deviations.Add(new DeviationDetail(
                CollectionDeviationType.MetadataMismatch, DeviationSeverity.Warning,
                e.RegisterFolderId, match.CollectionInstanceId, e.FullPath, match.FullPath,
                $"Governance metadata differs from the register: {string.Join("; ", mismatches)}.",
                "Reconcile governance in the register, then regenerate/reconcile."));
        }
    }

    private static void CheckMeta(ReadBackNode match, string key, string? expected, List<string> mismatches)
    {
        // Only compare when the provider reports the field (absent → not asserted in this FU).
        if (!match.Metadata.TryGetValue(key, out var actual))
        {
            return;
        }

        if (!string.Equals((expected ?? string.Empty).Trim(), (actual ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            mismatches.Add($"{key} expected '{expected}' but found '{actual}'");
        }
    }

    private static void DetectDuplicateFullPaths(IReadOnlyList<ReadBackNode> actual, List<DeviationDetail> deviations)
    {
        foreach (var group in actual.GroupBy(a => PathKey(a.FullPath), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            var first = group.First();
            deviations.Add(new DeviationDetail(
                CollectionDeviationType.DuplicateFullPath, DeviationSeverity.Critical,
                first.RegisterFolderId, first.CollectionInstanceId, first.FullPath, first.FullPath,
                $"Full path '{first.FullPath}' occurs {group.Count()} times in the live tree.",
                "Merge/remove duplicates through a governed change; full_path must be unique."));
        }
    }

    private static void DetectDuplicateSiblings(IReadOnlyList<ReadBackNode> actual, List<DeviationDetail> deviations)
    {
        foreach (var group in actual
            .GroupBy(a => $"{PathKey(a.ParentFullPath)}||{a.Name.Trim().ToLowerInvariant()}", StringComparer.Ordinal)
            .Where(g => g.Count() > 1))
        {
            var first = group.First();
            deviations.Add(new DeviationDetail(
                CollectionDeviationType.DuplicateSibling, DeviationSeverity.Major,
                first.RegisterFolderId, first.CollectionInstanceId, first.FullPath, first.FullPath,
                $"Sibling name '{first.Name}' occurs {group.Count()} times under the same parent.",
                "Resolve the duplicate sibling through a governed change."));
        }
    }

    private static void DetectOrphans(IReadOnlyList<ReadBackNode> actual, List<DeviationDetail> deviations)
    {
        var paths = new HashSet<string>(actual.Select(a => PathKey(a.FullPath)), StringComparer.OrdinalIgnoreCase);
        foreach (var a in actual.Where(a => !string.IsNullOrWhiteSpace(a.ParentFullPath)))
        {
            if (!paths.Contains(PathKey(a.ParentFullPath)))
            {
                deviations.Add(new DeviationDetail(
                    CollectionDeviationType.OrphanFolder, DeviationSeverity.Major,
                    a.RegisterFolderId, a.CollectionInstanceId, a.FullPath, a.FullPath,
                    $"Folder '{a.FullPath}' references missing parent '{a.ParentFullPath}'.",
                    "Provision or reconcile the parent; every non-root node needs an existing parent."));
            }
        }
    }

    private static string PathKey(string? path) => (path ?? string.Empty).Trim().ToLowerInvariant();

    private static string ActualKey(ReadBackNode n) =>
        !string.IsNullOrWhiteSpace(n.RegisterFolderId) ? $"fid:{n.RegisterFolderId.Trim().ToLowerInvariant()}" : $"path:{PathKey(n.FullPath)}";
}
