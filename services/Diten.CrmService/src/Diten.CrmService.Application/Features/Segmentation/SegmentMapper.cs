using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation;

/// <summary>Entity to DTO projection for the Segmentation feature. Read-only and total: it never touches an entity and
/// never invents a field. The list projection deliberately drops the criteria array and exposes counters instead, so a
/// grid read stays cheap.</summary>
public static class SegmentMapper
{
    public static SegmentListItemDto ToListItem(Segment segment) => new(
        segment.Id,
        segment.SegmentCode,
        segment.SegmentName,
        segment.SegmentType,
        segment.SubjectType,
        segment.SegmentStatus,
        segment.SegmentVersion,
        segment.VersionLineageId,
        segment.IsSuperseded(),
        segment.SupersededBySegmentId,
        segment.BusinessUnitId,
        segment.Description,
        segment.EffectiveFrom,
        segment.EffectiveTo,
        segment.MatchMode,
        segment.Criteria.Count,
        segment.Criteria.Count(n => n.IsPredicate()),
        segment.IsCriteriaFrozen(),
        segment.CriteriaFrozenAt,
        segment.ActivatedAt,
        segment.IsArchived(),
        segment.Version,
        segment.CreatedAt,
        segment.UpdatedAt);

    public static SegmentDetailDto ToDetail(Segment segment) => new(
        segment.Id,
        segment.SegmentCode,
        segment.SegmentName,
        segment.SegmentType,
        segment.SubjectType,
        segment.SegmentStatus,
        segment.SegmentVersion,
        segment.VersionLineageId,
        segment.IsSuperseded(),
        segment.SupersededBySegmentId,
        segment.BusinessUnitId,
        segment.Description,
        segment.Notes,
        segment.EffectiveFrom,
        segment.EffectiveTo,
        segment.MatchMode,
        segment.Criteria
            .OrderBy(n => n.SortOrder)
            .Select(ToCriteriaNode)
            .ToList(),
        segment.IsCriteriaFrozen(),
        segment.CriteriaFrozenAt,
        segment.ActivatedAt,
        segment.ActivatedBy,
        segment.ArchivedAt,
        segment.ArchivedBy,
        segment.IsArchived(),
        segment.Version,
        segment.CreatedAt,
        segment.CreatedBy,
        segment.UpdatedAt,
        segment.UpdatedBy);

    public static SegmentCriteriaNodeDto ToCriteriaNode(SegmentCriteriaNode node) => new(
        node.NodeId,
        node.ParentNodeId,
        node.NodeKind,
        node.GroupOperator,
        node.AttributeCode,
        node.Operator,
        node.Values,
        node.ValueType,
        node.Parameters,
        node.Negate,
        node.SortOrder,
        node.Label);

    public static TargetCustomerDto ToTargetCustomer(TargetCustomer target) => new(
        target.Id,
        target.SegmentId,
        target.SubjectType,
        target.SubjectId,
        target.MembershipMode,
        target.SubjectDisplayName,
        target.SelectionReason,
        target.ReasonCodes,
        target.EffectiveFrom,
        target.EffectiveTo,
        target.Notes,
        target.IsArchived(),
        target.ArchivedAt,
        target.Version,
        target.CreatedAt,
        target.CreatedBy,
        target.UpdatedAt,
        target.UpdatedBy);

    /// <summary>Materialises the criteria tree from the write model. NodeIds are ALWAYS assigned by the runtime and the
    /// parent references are remapped onto them, so a caller can never smuggle in an id from another segment (or reuse
    /// one from a previous version and quietly link two trees together).</summary>
    public static List<SegmentCriteriaNode> ToCriteria(IReadOnlyList<SegmentCriteriaNodeInput>? input)
    {
        var nodes = input ?? Array.Empty<SegmentCriteriaNodeInput>();
        if (nodes.Count == 0)
        {
            return new List<SegmentCriteriaNode>();
        }

        var idMap = new Dictionary<Guid, Guid>();
        foreach (var node in nodes)
        {
            var incoming = node.NodeId ?? Guid.NewGuid();
            if (!idMap.ContainsKey(incoming))
            {
                idMap[incoming] = Guid.NewGuid();
            }
        }

        var result = new List<SegmentCriteriaNode>(nodes.Count);
        foreach (var node in nodes)
        {
            var incoming = node.NodeId ?? Guid.Empty;
            var assigned = incoming != Guid.Empty && idMap.TryGetValue(incoming, out var mapped)
                ? mapped
                : Guid.NewGuid();

            result.Add(new SegmentCriteriaNode
            {
                NodeId = assigned,
                ParentNodeId = node.ParentNodeId is { } parent && idMap.TryGetValue(parent, out var mappedParent)
                    ? mappedParent
                    : null,
                NodeKind = SegmentCriteriaNodeKinds.Normalize(node.NodeKind),
                GroupOperator = node.GroupOperator is null
                    ? null
                    : SegmentGroupOperators.Normalize(node.GroupOperator),
                AttributeCode = SegmentValidation.Trim(node.AttributeCode)?.ToLowerInvariant(),
                Operator = node.Operator is null ? null : SegmentOperators.Normalize(node.Operator),
                Values = (node.Values ?? Array.Empty<string>())
                    .Where(v => v is not null)
                    .Select(v => v.Trim())
                    .ToList(),
                ValueType = node.ValueType is null ? null : SegmentValueTypes.Normalize(node.ValueType),
                Parameters = node.Parameters is null
                    ? new Dictionary<string, string>()
                    : node.Parameters
                        .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                        .ToDictionary(p => p.Key.Trim(), p => p.Value.Trim()),
                Negate = node.Negate,
                SortOrder = node.SortOrder,
                Label = SegmentValidation.Trim(node.Label)
            });
        }

        return result;
    }

    /// <summary>Clones a criteria tree for a new version: brand-new NodeIds with every ParentNodeId remapped onto
    /// them. Without the remap the clone would still point at the previous version tree and an edit to one version
    /// would silently rewrite the history of the other.</summary>
    public static List<SegmentCriteriaNode> CloneCriteria(IReadOnlyList<SegmentCriteriaNode> source)
    {
        var idMap = source.ToDictionary(n => n.NodeId, _ => Guid.NewGuid());

        return source.Select(n => new SegmentCriteriaNode
        {
            NodeId = idMap[n.NodeId],
            ParentNodeId = n.ParentNodeId is { } parent && idMap.TryGetValue(parent, out var mapped)
                ? mapped
                : null,
            NodeKind = n.NodeKind,
            GroupOperator = n.GroupOperator,
            AttributeCode = n.AttributeCode,
            Operator = n.Operator,
            Values = new List<string>(n.Values),
            ValueType = n.ValueType,
            Parameters = new Dictionary<string, string>(n.Parameters),
            Negate = n.Negate,
            SortOrder = n.SortOrder,
            Label = n.Label
        }).ToList();
    }

    /// <summary>True when the two trees differ in any way that changes what the rule ASKS. Used by the freeze guard so
    /// an update that leaves the criteria untouched is not rejected merely for resending them.</summary>
    public static bool CriteriaDiffer(
        IReadOnlyList<SegmentCriteriaNode> left, IReadOnlyList<SegmentCriteriaNode> right)
    {
        if (left.Count != right.Count)
        {
            return true;
        }

        static string Signature(SegmentCriteriaNode n, IReadOnlyList<SegmentCriteriaNode> all)
        {
            // Structural signature: position in the tree plus the question asked. NodeIds are excluded on purpose,
            // because a re-sent tree legitimately carries fresh ids.
            var depth = 0;
            var cursor = n.ParentNodeId;
            var guard = 0;
            while (cursor is { } parentId && guard++ < 16)
            {
                depth++;
                cursor = all.FirstOrDefault(x => x.NodeId == parentId)?.ParentNodeId;
            }

            var parameters = string.Join(";", n.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={p.Value}"));

            return string.Join("|",
                depth, n.SortOrder, n.NodeKind, n.GroupOperator, n.AttributeCode, n.Operator, n.ValueType,
                n.Negate, string.Join(",", n.Values), parameters);
        }

        var leftSignatures = left.Select(n => Signature(n, left)).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var rightSignatures = right.Select(n => Signature(n, right)).OrderBy(s => s, StringComparer.Ordinal).ToList();

        return !leftSignatures.SequenceEqual(rightSignatures, StringComparer.Ordinal);
    }
}
