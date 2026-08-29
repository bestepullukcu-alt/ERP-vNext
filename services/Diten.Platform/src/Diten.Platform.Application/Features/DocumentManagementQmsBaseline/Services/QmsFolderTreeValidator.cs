namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Builds and validates a normalized QMS folder tree from raw import rows. Pure and deterministic: the same rows,
/// tenant, and source baseline key always yield the same plan (canonical ids, ordering, per-definition hashes, and
/// findings). No persistence, no MOD-0220 call, no file-system access.
/// </summary>
public sealed class QmsFolderTreeValidator
{
    public QmsBaselineImportPlan BuildPlan(
        IReadOnlyList<QmsFolderImportRow> rows,
        Guid tenantId,
        string sourceBaselineKey)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var duplicateConflicts = new List<string>();
        var hierarchyFindings = new List<string>();
        var skipped = 0;

        // key (case-insensitive full path) -> node
        var nodes = new Dictionary<string, NodeBuild>(StringComparer.Ordinal);
        var orderedKeys = new List<string>();

        foreach (var row in rows.OrderBy(r => r.SourceRowNumber))
        {
            var rawSegments = ResolveRawSegments(row);
            if (rawSegments.Count == 0)
            {
                skipped++;
                errors.Add($"row {row.SourceRowNumber}: missing folder path/name");
                continue;
            }

            var normalizedSegments = new List<string>(rawSegments.Count);
            var segmentInvalid = false;
            foreach (var rawSegment in rawSegments)
            {
                if (!QmsFolderPathNormalizer.TryNormalizeSegment(rawSegment, out var normalized, out var error))
                {
                    hierarchyFindings.Add($"row {row.SourceRowNumber}: {error} ('{rawSegment}')");
                    segmentInvalid = true;
                    break;
                }

                normalizedSegments.Add(normalized);
            }

            if (segmentInvalid)
            {
                skipped++;
                continue;
            }

            var fullPath = QmsFolderPathNormalizer.BuildFullPath(normalizedSegments);
            var key = QmsFolderPathNormalizer.CaseInsensitiveKey(fullPath);

            if (nodes.ContainsKey(key))
            {
                duplicateConflicts.Add($"row {row.SourceRowNumber}: duplicate sibling path '{fullPath}'");
                skipped++;
                continue;
            }

            var node = new NodeBuild(row, normalizedSegments, fullPath, key);
            nodes[key] = node;
            orderedKeys.Add(key);
        }

        // Hierarchy + cycle validation now that all nodes are known.
        foreach (var key in orderedKeys)
        {
            var node = nodes[key];
            if (node.Segments.Count == 1)
            {
                continue;
            }

            var parentFullPath = QmsFolderPathNormalizer.BuildFullPath(node.Segments.Take(node.Segments.Count - 1));
            var parentKey = QmsFolderPathNormalizer.CaseInsensitiveKey(parentFullPath);

            if (!nodes.ContainsKey(parentKey))
            {
                hierarchyFindings.Add($"row {node.Row.SourceRowNumber}: missing parent for '{node.FullPath}' (gap at '{parentFullPath}')");
                node.HasGap = true;
                continue;
            }

            // Path-derived trees are structurally acyclic; guard against a self/descendant parent regardless.
            if (string.Equals(parentKey, key, StringComparison.Ordinal)
                || parentKey.StartsWith(key + QmsFolderPathNormalizer.Separator, StringComparison.Ordinal))
            {
                hierarchyFindings.Add($"row {node.Row.SourceRowNumber}: cycle detected at '{node.FullPath}'");
                node.HasGap = true;
            }
        }

        var hasBlockingFindings = errors.Count > 0 || duplicateConflicts.Count > 0 || hierarchyFindings.Count > 0;

        // Deterministic output order: DisplayOrder (explicit or source), then full path.
        var orderedNodes = orderedKeys
            .Select(k => nodes[k])
            .Where(n => !n.HasGap)
            .OrderBy(n => n.Row.DisplayOrder ?? n.Row.SourceRowNumber)
            .ThenBy(n => n.Key, StringComparer.Ordinal)
            .ToList();

        var definitions = new List<QmsCollectionDefinitionDraft>(orderedNodes.Count);
        if (!hasBlockingFindings)
        {
            // QMS register import extension — governance identity pending. Resolve each node's CanonicalId up front
            // so parent linkage reuses the child's own
            // strategy. A node that carries a register folder_id gets a stable, path-independent id; every other
            // node keeps the exact legacy path-hash id. Parent resolution stays path-based (unchanged); only the
            // id VALUE changes, and only for register rows.
            var canonicalByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                canonicalByKey[node.Key] = ResolveCanonicalId(node.Row, node.FullPath, tenantId, sourceBaselineKey);
            }

            var display = 0;
            foreach (var node in orderedNodes)
            {
                var canonicalId = canonicalByKey[node.Key];
                string? parentCanonicalId = null;
                string? parentKey = null;
                if (node.Segments.Count > 1)
                {
                    var parentFullPath = QmsFolderPathNormalizer.BuildFullPath(node.Segments.Take(node.Segments.Count - 1));
                    parentKey = QmsFolderPathNormalizer.CaseInsensitiveKey(parentFullPath);
                    parentCanonicalId = canonicalByKey.TryGetValue(parentKey, out var pc)
                        ? pc
                        : QmsCanonicalIdFactory.Create(tenantId, sourceBaselineKey, parentFullPath);
                }

                var draft = new QmsCollectionDefinitionDraft(
                    canonicalId,
                    parentCanonicalId,
                    node.Segments[^1],
                    node.Row.PurposeScope?.Trim(),
                    node.Row.RequiredByScope?.Trim(),
                    node.Row.AllowsManualChildren ?? false,
                    node.Row.TemplatesAllowed ?? false,
                    node.Row.AllowedDocClass?.Trim(),
                    node.Row.DefaultClassificationLevel?.Trim(),
                    node.Row.DefaultRetentionHint?.Trim(),
                    node.Row.IsMandatory ?? false,
                    node.Row.IsAutoProvisioned ?? false,
                    node.Row.IsProtected ?? false,
                    node.Segments[^1],
                    node.FullPath,
                    display++,
                    DefinitionHash: string.Empty);

                // Structural hash first (register governance is deliberately excluded), then attach the
                // register-backed governance metadata carried by the row.
                definitions.Add((draft with { DefinitionHash = QmsStructuralHasher.HashDefinition(draft) })
                    with
                    {
                        RegisterFolderId = Trimmed(node.Row.FolderId),
                        RegisterParentFolderId = Trimmed(node.Row.ParentFolderId),
                        RegisterFullPath = Trimmed(node.Row.RegisterFullPath) ?? node.FullPath,
                        DepartmentDomain = Trimmed(node.Row.DepartmentDomain),
                        FolderType = Trimmed(node.Row.FolderType),
                        ExampleDocuments = Trimmed(node.Row.ExampleDocuments),
                        OwningDepartments = Trimmed(node.Row.OwningDepartments),
                        ControlledByGqms = Trimmed(node.Row.ControlledByGqms),
                        SourceOfTruth = Trimmed(node.Row.SourceOfTruth),
                        OwnerFunction = Trimmed(node.Row.OwnerFunction),
                        AccessProfile = Trimmed(node.Row.AccessProfile),
                        RetentionClass = Trimmed(node.Row.RetentionClass),
                        ChangeControlRequired = Trimmed(node.Row.ChangeControlRequired),
                        GqmsScopeLink = Trimmed(node.Row.GqmsScopeLink),
                        LegacyCode = Trimmed(node.Row.LegacyCode),
                        ProvisioningWave = Trimmed(node.Row.ProvisioningWave),
                        ProvisioningOrder = node.Row.ProvisioningOrder
                    });
            }
        }

        var summary = new QmsBaselineImportSummary(
            TotalRows: rows.Count,
            ImportedDefinitionsCount: definitions.Count,
            SkippedRows: skipped,
            Errors: errors,
            Warnings: warnings,
            DuplicatePathConflicts: duplicateConflicts,
            InvalidHierarchyFindings: hierarchyFindings,
            DryRun: true,
            Committed: false);

        return new QmsBaselineImportPlan(summary, definitions);
    }

    /// <summary>
    /// Register-backed identity when the row carries a stable <c>folder_id</c>; otherwise the legacy path-hash id.
    /// Keeps existing (folder_id-less) imports byte-for-byte identical.
    /// </summary>
    private static string ResolveCanonicalId(QmsFolderImportRow row, string fullPath, Guid tenantId, string sourceBaselineKey) =>
        string.IsNullOrWhiteSpace(row.FolderId)
            ? QmsCanonicalIdFactory.Create(tenantId, sourceBaselineKey, fullPath)
            : QmsCanonicalIdFactory.CreateFromRegisterFolderId(tenantId, sourceBaselineKey, row.FolderId);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> ResolveRawSegments(QmsFolderImportRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.Path))
        {
            return QmsFolderPathNormalizer.SplitPath(row.Path);
        }

        if (!string.IsNullOrWhiteSpace(row.Name))
        {
            var parent = QmsFolderPathNormalizer.SplitPath(row.ParentPath);
            return [.. parent, row.Name.Trim()];
        }

        return [];
    }

    private sealed class NodeBuild(QmsFolderImportRow row, List<string> segments, string fullPath, string key)
    {
        public QmsFolderImportRow Row { get; } = row;
        public List<string> Segments { get; } = segments;
        public string FullPath { get; } = fullPath;
        public string Key { get; } = key;
        public bool HasGap { get; set; }
    }
}
