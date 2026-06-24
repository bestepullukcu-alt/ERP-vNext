using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// FU04 manual tree operations. CanonicalId is stable node identity; FullPath is recalculated deterministically
/// from ancestor path segments when a node is renamed or moved.
/// </summary>
public sealed class QmsManualStructureService
{
    public QmsManualTreeValidationResult ValidateTree(IReadOnlyList<CollectionDefinition> definitions)
    {
        var active = definitions.Where(d => !d.IsDeleted).ToList();
        var errors = new List<string>();
        var warnings = new List<string>();
        var duplicateSiblingFindings = new List<string>();
        var orphanParentFindings = new List<string>();
        var invalidHierarchyFindings = new List<string>();

        var byCanonical = active.ToDictionary(d => d.CanonicalId, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in active)
        {
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                errors.Add($"Definition '{definition.CanonicalId}' has an empty name.");
            }

            if (!QmsFolderPathNormalizer.TryNormalizeAtomicName(definition.Name, out _, out var nameError))
            {
                errors.Add($"Definition '{definition.CanonicalId}' has an invalid name: {nameError}.");
            }

            if (!string.IsNullOrWhiteSpace(definition.ParentCanonicalId)
                && !byCanonical.ContainsKey(definition.ParentCanonicalId))
            {
                orphanParentFindings.Add($"Definition '{definition.CanonicalId}' references a missing parent.");
            }
        }

        var duplicates = active
            .GroupBy(d => new
            {
                Parent = d.ParentCanonicalId ?? string.Empty,
                Segment = QmsFolderPathNormalizer.CaseInsensitiveKey(d.PathSegment)
            })
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var duplicate in duplicates)
        {
            duplicateSiblingFindings.Add($"Duplicate sibling folder name '{duplicate.First().PathSegment}'.");
        }

        foreach (var definition in active)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { definition.CanonicalId };
            var parent = definition.ParentCanonicalId;
            while (!string.IsNullOrWhiteSpace(parent))
            {
                if (!seen.Add(parent))
                {
                    invalidHierarchyFindings.Add($"Cycle detected at definition '{definition.CanonicalId}'.");
                    break;
                }

                if (!byCanonical.TryGetValue(parent, out var parentDefinition))
                {
                    break;
                }

                parent = parentDefinition.ParentCanonicalId;
            }
        }

        return new QmsManualTreeValidationResult(
            errors.Count == 0 && duplicateSiblingFindings.Count == 0 && orphanParentFindings.Count == 0 && invalidHierarchyFindings.Count == 0,
            errors,
            warnings,
            duplicateSiblingFindings,
            orphanParentFindings,
            invalidHierarchyFindings);
    }

    public QmsManualOperationResult<CollectionDefinition> CreateDefinition(
        Guid tenantId,
        BaselineRelease baseline,
        QmsCollectionDefinitionUpsertModel request,
        IReadOnlyList<CollectionDefinition> existing)
    {
        var normalizedName = NormalizeName(request.Name);
        if (!normalizedName.Success)
        {
            return QmsManualOperationResult<CollectionDefinition>.Validation(normalizedName.Error!);
        }

        var normalizedParent = NormalizeParent(request.ParentCanonicalId);
        var parent = ResolveParent(normalizedParent, existing);
        if (!string.IsNullOrWhiteSpace(normalizedParent) && parent is null)
        {
            return QmsManualOperationResult<CollectionDefinition>.NotFound("Parent definition not found.");
        }

        if (HasDuplicateSibling(existing, normalizedParent, normalizedName.Value!, excludeCanonicalId: null))
        {
            return QmsManualOperationResult<CollectionDefinition>.Conflict("Duplicate sibling folder name.");
        }

        var fullPath = BuildFullPath(parent, normalizedName.Value!);
        var canonicalId = QmsCanonicalIdFactory.Create(tenantId, baseline.SourceBaselineKey, fullPath);
        if (existing.Any(d => string.Equals(d.CanonicalId, canonicalId, StringComparison.OrdinalIgnoreCase)))
        {
            return QmsManualOperationResult<CollectionDefinition>.Conflict("Duplicate canonical definition.");
        }

        var definition = new CollectionDefinition
        {
            TenantId = tenantId,
            CanonicalId = canonicalId,
            ParentCanonicalId = normalizedParent,
            BaselineReleaseId = baseline.Id,
            Name = normalizedName.Value!,
            PurposeScope = TrimOrNull(request.PurposeScope),
            RequiredByScope = TrimOrNull(request.RequiredByScope),
            AllowsManualChildren = request.AllowsManualChildren,
            TemplatesAllowed = request.TemplatesAllowed,
            AllowedDocClass = TrimOrNull(request.AllowedDocClass),
            DefaultClassificationLevel = TrimOrNull(request.DefaultClassificationLevel),
            DefaultRetentionHint = TrimOrNull(request.DefaultRetentionHint),
            IsMandatory = request.IsMandatory,
            IsAutoProvisioned = false,
            IsProtected = request.IsProtected,
            PathSegment = normalizedName.Value!,
            FullPath = fullPath,
            DisplayOrder = request.DisplayOrder,
            DefinitionHash = string.Empty
        };
        definition.DefinitionHash = ComputeHash(definition);
        return QmsManualOperationResult<CollectionDefinition>.Ok(definition);
    }

    public QmsManualOperationResult<IReadOnlyList<CollectionDefinition>> UpdateDefinition(
        CollectionDefinition target,
        QmsCollectionDefinitionUpsertModel request,
        IReadOnlyList<CollectionDefinition> existing)
    {
        var normalizedName = NormalizeName(request.Name);
        if (!normalizedName.Success)
        {
            return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Validation(normalizedName.Error!);
        }

        var normalizedParent = NormalizeParent(request.ParentCanonicalId);
        if (!string.Equals(normalizedParent, target.ParentCanonicalId, StringComparison.OrdinalIgnoreCase))
        {
            return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Validation(
                "Parent changes must use the move endpoint.");
        }

        if (HasDuplicateSibling(existing, normalizedParent, normalizedName.Value!, target.CanonicalId))
        {
            return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Conflict("Duplicate sibling folder name.");
        }

        var willDisableManualChildren = target.AllowsManualChildren && !request.AllowsManualChildren;
        var childrenToPromote = willDisableManualChildren
            ? existing
                .Where(d =>
                    !d.IsDeleted
                    && string.Equals(d.ParentCanonicalId, target.CanonicalId, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];

        foreach (var child in childrenToPromote)
        {
            if (HasDuplicateSibling(existing, parentCanonicalId: null, child.PathSegment, child.CanonicalId))
            {
                return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Conflict(
                    $"Cannot move child '{child.PathSegment}' to root because a root node with the same name already exists.");
            }
        }

        target.Name = normalizedName.Value!;
        target.PathSegment = normalizedName.Value!;
        target.PurposeScope = TrimOrNull(request.PurposeScope);
        target.RequiredByScope = TrimOrNull(request.RequiredByScope);
        target.AllowedDocClass = TrimOrNull(request.AllowedDocClass);
        target.DefaultClassificationLevel = TrimOrNull(request.DefaultClassificationLevel);
        target.DefaultRetentionHint = TrimOrNull(request.DefaultRetentionHint);
        target.DisplayOrder = request.DisplayOrder;
        target.AllowsManualChildren = request.AllowsManualChildren;
        target.TemplatesAllowed = request.TemplatesAllowed;
        target.IsMandatory = request.IsMandatory;
        target.IsProtected = request.IsProtected;

        foreach (var child in childrenToPromote)
        {
            child.ParentCanonicalId = null;
        }

        var changed = RecalculateSubtree(target, existing).ToList();
        foreach (var child in childrenToPromote)
        {
            changed.AddRange(RecalculateSubtree(child, existing));
        }

        changed = changed
            .GroupBy(d => d.CanonicalId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Ok(changed);
    }

    public QmsManualOperationResult<IReadOnlyList<CollectionDefinition>> MoveDefinition(
        CollectionDefinition target,
        QmsCollectionDefinitionMoveModel request,
        IReadOnlyList<CollectionDefinition> existing)
    {
        var normalizedParent = NormalizeParent(request.ParentCanonicalId);
        var parent = ResolveParent(normalizedParent, existing);
        if (!string.IsNullOrWhiteSpace(normalizedParent) && parent is null)
        {
            return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.NotFound("Parent definition not found.");
        }

        if (string.Equals(normalizedParent, target.CanonicalId, StringComparison.OrdinalIgnoreCase)
            || IsDescendant(normalizedParent, target.CanonicalId, existing))
        {
            return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Validation("Move would create a cycle.");
        }

        if (!string.Equals(normalizedParent, target.ParentCanonicalId, StringComparison.OrdinalIgnoreCase)
            && parent is not null
            && !parent.AllowsManualChildren)
        {
            return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Validation("Target parent does not allow manual children.");
        }

        if (HasDuplicateSibling(existing, normalizedParent, target.PathSegment, target.CanonicalId))
        {
            return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Conflict("Duplicate sibling folder name.");
        }

        target.ParentCanonicalId = normalizedParent;
        target.DisplayOrder = request.DisplayOrder;
        var changed = RecalculateSubtree(target, existing);
        return QmsManualOperationResult<IReadOnlyList<CollectionDefinition>>.Ok(changed);
    }

    public string ComputeHash(CollectionDefinition definition)
    {
        var draft = new QmsCollectionDefinitionDraft(
            definition.CanonicalId,
            definition.ParentCanonicalId,
            definition.Name,
            definition.PurposeScope,
            definition.RequiredByScope,
            definition.AllowsManualChildren,
            definition.TemplatesAllowed,
            definition.AllowedDocClass,
            definition.DefaultClassificationLevel,
            definition.DefaultRetentionHint,
            definition.IsMandatory,
            definition.IsAutoProvisioned,
            definition.IsProtected,
            definition.PathSegment,
            definition.FullPath,
            definition.DisplayOrder,
            string.Empty);
        return QmsStructuralHasher.HashDefinition(draft);
    }

    private IReadOnlyList<CollectionDefinition> RecalculateSubtree(
        CollectionDefinition root,
        IReadOnlyList<CollectionDefinition> existing)
    {
        var active = existing.Where(d => !d.IsDeleted).ToList();
        var byCanonical = active.ToDictionary(d => d.CanonicalId, StringComparer.OrdinalIgnoreCase);
        var children = active
            .GroupBy(d => d.ParentCanonicalId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var changed = new List<CollectionDefinition>();

        void Visit(CollectionDefinition node)
        {
            CollectionDefinition? parent = null;
            if (!string.IsNullOrWhiteSpace(node.ParentCanonicalId))
            {
                byCanonical.TryGetValue(node.ParentCanonicalId, out parent);
            }

            node.FullPath = BuildFullPath(parent, node.PathSegment);
            node.DefinitionHash = ComputeHash(node);
            changed.Add(node);

            if (!children.TryGetValue(node.CanonicalId, out var directChildren))
            {
                return;
            }

            foreach (var child in directChildren)
            {
                Visit(child);
            }
        }

        Visit(root);
        return changed;
    }

    private static bool HasDuplicateSibling(
        IReadOnlyList<CollectionDefinition> existing,
        string? parentCanonicalId,
        string pathSegment,
        string? excludeCanonicalId)
    {
        var parent = parentCanonicalId ?? string.Empty;
        var segment = QmsFolderPathNormalizer.CaseInsensitiveKey(pathSegment);
        return existing.Any(d =>
            !d.IsDeleted
            && !string.Equals(d.CanonicalId, excludeCanonicalId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(d.ParentCanonicalId ?? string.Empty, parent, StringComparison.OrdinalIgnoreCase)
            && QmsFolderPathNormalizer.CaseInsensitiveKey(d.PathSegment) == segment);
    }

    private static bool IsDescendant(string? possibleDescendantCanonicalId, string ancestorCanonicalId, IReadOnlyList<CollectionDefinition> existing)
    {
        if (string.IsNullOrWhiteSpace(possibleDescendantCanonicalId))
        {
            return false;
        }

        var byCanonical = existing.ToDictionary(d => d.CanonicalId, StringComparer.OrdinalIgnoreCase);
        var current = possibleDescendantCanonicalId;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (string.Equals(current, ancestorCanonicalId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!byCanonical.TryGetValue(current, out var node))
            {
                return false;
            }

            current = node.ParentCanonicalId;
        }

        return false;
    }

    private static CollectionDefinition? ResolveParent(string? parentCanonicalId, IReadOnlyList<CollectionDefinition> existing) =>
        string.IsNullOrWhiteSpace(parentCanonicalId)
            ? null
            : existing.FirstOrDefault(d =>
                !d.IsDeleted && string.Equals(d.CanonicalId, parentCanonicalId, StringComparison.OrdinalIgnoreCase));

    private static string BuildFullPath(CollectionDefinition? parent, string pathSegment) =>
        parent is null || string.IsNullOrWhiteSpace(parent.FullPath)
            ? pathSegment
            : QmsFolderPathNormalizer.BuildFullPath([parent.FullPath, pathSegment]);

    private static (bool Success, string? Value, string? Error) NormalizeName(string? raw)
    {
        if (!QmsFolderPathNormalizer.TryNormalizeAtomicName(raw, out var normalized, out var error))
        {
            return (false, null, error);
        }

        return (true, normalized, null);
    }

    private static string? NormalizeParent(string? raw) => TrimOrNull(raw);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record QmsManualTreeValidationResult(
    bool Valid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> DuplicateSiblingFindings,
    IReadOnlyList<string> OrphanParentFindings,
    IReadOnlyList<string> InvalidHierarchyFindings);

public sealed record QmsManualOperationResult<T>(
    bool Success,
    T? Value,
    int StatusCode,
    string ReasonCode,
    IReadOnlyList<string> Errors)
{
    public static QmsManualOperationResult<T> Ok(T value) =>
        new(true, value, 200, string.Empty, []);

    public static QmsManualOperationResult<T> Validation(string error) =>
        new(false, default, 400, QmsBaselineReasonCodes.ValidationFailed, [error]);

    public static QmsManualOperationResult<T> Conflict(string error) =>
        new(false, default, 409, QmsBaselineReasonCodes.Conflict, [error]);

    public static QmsManualOperationResult<T> NotFound(string error) =>
        new(false, default, 404, QmsBaselineReasonCodes.NotFoundNonLeakage, [error]);
}
