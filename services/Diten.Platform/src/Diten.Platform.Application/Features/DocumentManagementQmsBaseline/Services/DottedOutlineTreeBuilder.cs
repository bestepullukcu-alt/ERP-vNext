using System.Globalization;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Builds and validates a nested tree from the canonical "last version" sheet's dotted outline codes. Pure and
/// deterministic. Parent resolution is by NUMERIC normalization of the code (each '.'-segment is integer-parsed,
/// stripping leading zeros) so <c>00.01.01</c> resolves to parent <c>0.01</c> and <c>0.01</c> to <c>0</c>. Folder
/// names are treated as atomic segments (never split), because real QMS names contain '/'. The dotted code is never
/// used as a name or path segment; <c>FullPath</c> is server-derived by joining resolved ancestor names.
/// </summary>
public sealed class DottedOutlineTreeBuilder
{
    private const string RootSibToken = "<root>";

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

        var nodes = new Dictionary<string, OutlineNode>(StringComparer.Ordinal);
        var orderedKeys = new List<string>();

        foreach (var row in rows.OrderBy(r => r.SourceRowNumber))
        {
            var code = row.OutlineCode?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add($"row {row.SourceRowNumber}: missing outline code");
                skipped++;
                continue;
            }

            var key = NormalizeCodeKey(code);
            if (key is null)
            {
                hierarchyFindings.Add($"row {row.SourceRowNumber}: non-numeric outline code '{code}'");
                skipped++;
                continue;
            }

            if (!QmsFolderPathNormalizer.TryNormalizeAtomicName(row.Name, out var name, out var nameError))
            {
                hierarchyFindings.Add($"row {row.SourceRowNumber}: {nameError} ('{row.Name}')");
                skipped++;
                continue;
            }

            if (nodes.ContainsKey(key))
            {
                duplicateConflicts.Add($"row {row.SourceRowNumber}: duplicate outline code '{code}'");
                skipped++;
                continue;
            }

            nodes[key] = new OutlineNode(row, name, key);
            orderedKeys.Add(key);
        }

        // Parent resolution + gap detection (numeric prefix parentage; cycles are structurally impossible).
        foreach (var key in orderedKeys)
        {
            var node = nodes[key];
            node.ParentKey = ParentKey(key);
            if (node.ParentKey is not null && !nodes.ContainsKey(node.ParentKey))
            {
                hierarchyFindings.Add(
                    $"row {node.Row.SourceRowNumber}: missing parent for outline code '{node.Row.OutlineCode}' (expected parent key '{node.ParentKey}')");
                node.HasGap = true;
            }
        }

        // Duplicate sibling: same parent + same (case-insensitive) folder name.
        var seenSiblings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in orderedKeys)
        {
            var node = nodes[key];
            if (node.HasGap)
            {
                continue;
            }

            var siblingKey = $"{node.ParentKey ?? RootSibToken}{node.Name.ToLowerInvariant()}";
            if (!seenSiblings.Add(siblingKey))
            {
                duplicateConflicts.Add(
                    $"row {node.Row.SourceRowNumber}: duplicate sibling '{node.Name}' under parent '{node.ParentKey ?? RootSibToken}'");
            }
        }

        var hasBlockingFindings = errors.Count > 0 || duplicateConflicts.Count > 0 || hierarchyFindings.Count > 0;

        var definitions = new List<QmsCollectionDefinitionDraft>(orderedKeys.Count);
        if (!hasBlockingFindings)
        {
            var display = 0;
            foreach (var key in orderedKeys) // source order preserved
            {
                var node = nodes[key];
                var fullPath = ResolveFullPath(nodes, key);
                string? parentCanonicalId = null;
                if (node.ParentKey is not null)
                {
                    var parentFullPath = ResolveFullPath(nodes, node.ParentKey);
                    parentCanonicalId = QmsCanonicalIdFactory.Create(tenantId, sourceBaselineKey, parentFullPath);
                }

                var draft = new QmsCollectionDefinitionDraft(
                    QmsCanonicalIdFactory.Create(tenantId, sourceBaselineKey, fullPath),
                    parentCanonicalId,
                    node.Name,
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
                    node.Name,
                    fullPath,
                    display++,
                    DefinitionHash: string.Empty);

                definitions.Add(draft with { DefinitionHash = QmsStructuralHasher.HashDefinition(draft) });
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
    /// Canonical key for an outline code. Every segment must be numeric. The workbook mixes text-like dotted
    /// outline keys with Excel floating artifacts: <c>7.0000000000000007E-2</c> means <c>0.07</c>,
    /// <c>1.1000000000000001</c> means <c>1.10</c>, and literal depth-2 decimals such as <c>6.1</c> mean
    /// <c>6.10</c>. At depth 3+, a one-digit middle segment is treated as missing left padding
    /// (<c>06.3.03</c> -> <c>6.03.03</c>). Returns null if any segment is non-numeric.
    /// </summary>
    internal static string? NormalizeCodeKey(string code)
    {
        code = NormalizeFloatingOutlineCode(code.Trim());
        var parts = code.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var normalized = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            normalized[i] = i switch
            {
                0 => value.ToString(CultureInfo.InvariantCulture),
                _ when parts[i].Length == 1 && parts.Length == 2 => parts[i] + "0",
                _ when parts[i].Length == 1 => parts[i].PadLeft(2, '0'),
                _ => parts[i]
            };
        }

        return string.Join('.', normalized);
    }

    private static string NormalizeFloatingOutlineCode(string code)
    {
        var singleDecimalSeparator = code.Count(ch => ch == '.') == 1;
        var hasFloatingArtifact = code.Contains('E', StringComparison.OrdinalIgnoreCase)
            || (singleDecimalSeparator && code[(code.IndexOf('.') + 1)..].Length > 2);
        if (!hasFloatingArtifact)
        {
            return code;
        }

        if (!decimal.TryParse(code, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return code;
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Parent key = the numeric key with its last segment removed; null for a single-segment (root) key.</summary>
    internal static string? ParentKey(string key)
    {
        var idx = key.LastIndexOf('.');
        return idx < 0 ? null : key[..idx];
    }

    private static string ResolveFullPath(Dictionary<string, OutlineNode> nodes, string key)
    {
        var names = new List<string>();
        var cur = (string?)key;
        while (cur is not null && nodes.TryGetValue(cur, out var node))
        {
            names.Insert(0, node.Name);
            cur = ParentKey(cur);
        }

        return QmsFolderPathNormalizer.BuildFullPath(names);
    }

    private sealed class OutlineNode(QmsFolderImportRow row, string name, string key)
    {
        public QmsFolderImportRow Row { get; } = row;
        public string Name { get; } = name;
        public string Key { get; } = key;
        public string? ParentKey { get; set; }
        public bool HasGap { get; set; }
    }
}
