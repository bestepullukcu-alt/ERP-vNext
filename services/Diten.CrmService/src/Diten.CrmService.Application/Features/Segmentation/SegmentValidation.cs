using System.Globalization;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation;

/// <summary>
/// MOD-0167 FU02 shared write-path validation: segment fields, the embedded criteria tree (D2) and the manual
/// membership row. Kept in ONE place so create / update / new-version can never drift apart.
/// <para>Everything here is <b>structural and in-domain</b> (D-VOCAB = A): an out-of-set value is a 400 and no MOD-0048
/// set is read, so authoring never fails open on an unpublished set and never blocks on an operator task. Nothing here
/// performs I/O — the cross-service value proof (class X) is a separate, explicitly fail-closed step the handler runs
/// BEFORE persisting.</para>
/// </summary>
public static class SegmentValidation
{
    /// <summary>A rejected write: a message for the human, a machine code for the UI/smoke script, and the status the
    /// handler must answer with. Nested so this file still declares a single top-level public type.</summary>
    public sealed record Failure(string Message, string? Code, int StatusCode = 400);

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ---------------------------------------------------------------------------------------------------------
    // Segment fields
    // ---------------------------------------------------------------------------------------------------------

    public static Failure? ValidateSegmentCode(string? segmentCode)
    {
        var code = Trim(segmentCode);
        if (code is null)
        {
            return new Failure("SegmentCode is required.", null);
        }

        if (code.Length > SegmentLimits.MaxSegmentCodeLength)
        {
            return new Failure(
                $"SegmentCode must be at most {SegmentLimits.MaxSegmentCodeLength} characters.", null);
        }

        return System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9][a-z0-9-]*$")
            ? null
            : new Failure("SegmentCode must be lowercase and may contain only letters, digits and hyphens.", null);
    }

    public static Failure? ValidateSegmentName(string? segmentName)
    {
        var name = Trim(segmentName);
        if (name is null)
        {
            return new Failure("SegmentName is required.", null);
        }

        return name.Length > SegmentLimits.MaxSegmentNameLength
            ? new Failure($"SegmentName must be at most {SegmentLimits.MaxSegmentNameLength} characters.", null)
            : null;
    }

    public static Failure? ValidateSegmentType(string? segmentType)
        => SegmentTypes.IsValid(segmentType)
            ? null
            : new Failure(
                $"SegmentType must be one of: {string.Join(", ", SegmentTypes.All)}.", null);

    public static Failure? ValidateSubjectType(string? subjectType)
        => SegmentSubjectTypes.IsValid(subjectType)
            ? null
            : new Failure(
                $"SubjectType must be one of: {string.Join(", ", SegmentSubjectTypes.All)}.", null);

    public static Failure? ValidateSegmentStatus(string? segmentStatus)
        => SegmentStatuses.IsValid(segmentStatus)
            ? null
            : new Failure(
                $"SegmentStatus must be one of: {string.Join(", ", SegmentStatuses.All)}.", null);

    public static Failure? ValidateMatchMode(string? matchMode)
        => SegmentMatchModes.IsValid(matchMode)
            ? null
            : new Failure(
                $"MatchMode must be one of: {string.Join(", ", SegmentMatchModes.AllValues)}.", null);

    public static Failure? ValidateEffectiveRange(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is { } to && to <= effectiveFrom
            ? new Failure("EffectiveTo must be later than EffectiveFrom.", null)
            : null;

    public static Failure? ValidateBusinessUnitId(string? businessUnitId)
    {
        if (businessUnitId is null)
        {
            return null;
        }

        var value = Trim(businessUnitId);
        if (value is null)
        {
            return new Failure("BusinessUnitId may be omitted, but it may not be blank.", null);
        }

        return value.Length > SegmentLimits.MaxBusinessUnitIdLength
            ? new Failure(
                $"BusinessUnitId must be at most {SegmentLimits.MaxBusinessUnitIdLength} characters.", null)
            : null;
    }

    public static Failure? ValidateFreeText(string? value, string fieldName, int maxLength)
        => value is not null && value.Trim().Length > maxLength
            ? new Failure($"{fieldName} must be at most {maxLength} characters.", null)
            : null;

    /// <summary>Legal transitions only: draft to active, draft to archived, active to archived. Anything else (notably
    /// archived back to active) is a 409.</summary>
    public static Failure? ValidateStatusTransition(string current, string next)
    {
        var from = SegmentStatuses.Normalize(current);
        var to = SegmentStatuses.Normalize(next);
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return null;
        }

        var allowed = (from, to) switch
        {
            (SegmentStatuses.Draft, SegmentStatuses.Active) => true,
            (SegmentStatuses.Draft, SegmentStatuses.Archived) => true,
            (SegmentStatuses.Active, SegmentStatuses.Archived) => true,
            _ => false
        };

        return allowed
            ? null
            : new Failure($"SegmentStatus cannot move from '{from}' to '{to}'.", null, 409);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Criteria tree (D2)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Validates the WHOLE embedded tree in a single pass: shape, catalog conformance, operator/value arity,
    /// required parameters, subject-type applicability, depth / node / child / value ceilings, sibling SortOrder
    /// uniqueness, empty groups, <c>not</c> arity and parent cycles. Every ceiling overflow is an explicit 400 — there
    /// is no silent truncation anywhere.</summary>
    public static Failure? ValidateCriteria(
        string segmentType, string subjectType, IReadOnlyList<SegmentCriteriaNode> nodes)
    {
        var type = SegmentTypes.Normalize(segmentType);
        var subject = SegmentSubjectTypes.Normalize(subjectType);

        if (string.Equals(type, SegmentTypes.Static, StringComparison.Ordinal))
        {
            return nodes.Count > 0
                ? new Failure(
                    "A static segment carries no criteria; its membership is the manual TargetCustomer list.", null)
                : null;
        }

        if (nodes.Count == 0 || nodes.All(n => !n.IsPredicate()))
        {
            return new Failure(
                $"A {type} segment requires at least one criteria predicate.", null);
        }

        if (nodes.Count > SegmentLimits.MaxCriteriaNodes)
        {
            return new Failure(
                $"A criteria tree may hold at most {SegmentLimits.MaxCriteriaNodes} nodes.", null);
        }

        var byId = new Dictionary<Guid, SegmentCriteriaNode>();
        foreach (var node in nodes)
        {
            if (node.NodeId == Guid.Empty)
            {
                return new Failure("Every criteria node needs a NodeId.", null);
            }

            if (!byId.TryAdd(node.NodeId, node))
            {
                return new Failure($"Duplicate criteria NodeId '{node.NodeId}'.", null);
            }
        }

        foreach (var node in nodes)
        {
            var nodeFailure = ValidateNode(node, subject);
            if (nodeFailure is not null)
            {
                return nodeFailure;
            }

            if (node.ParentNodeId is { } parentId && !byId.ContainsKey(parentId))
            {
                return new Failure(
                    $"Criteria node '{node.NodeId}' points at a ParentNodeId that is not part of this segment.", null);
            }
        }

        // Sibling rules: unique SortOrder, group child ceiling, no empty group, `not` takes exactly one child.
        // Guid.Empty stands for "child of the implicit root": a Dictionary cannot take a null key.
        var childrenByParent = nodes
            .GroupBy(n => n.ParentNodeId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var group in childrenByParent)
        {
            var siblings = group.Value;
            if (siblings.Select(s => s.SortOrder).Distinct().Count() != siblings.Count)
            {
                return new Failure("SortOrder must be unique among sibling criteria nodes.", null);
            }

            if (group.Key != Guid.Empty && siblings.Count > SegmentLimits.MaxChildrenPerGroup)
            {
                return new Failure(
                    $"A criteria group may hold at most {SegmentLimits.MaxChildrenPerGroup} children.", null);
            }
        }

        foreach (var node in nodes.Where(n => n.IsGroup()))
        {
            var childCount = childrenByParent.TryGetValue(node.NodeId, out var kids) ? kids.Count : 0;
            if (childCount == 0)
            {
                return new Failure($"Criteria group '{node.NodeId}' has no children.", null);
            }

            if (string.Equals(SegmentGroupOperators.Normalize(node.GroupOperator), SegmentGroupOperators.Not,
                    StringComparison.Ordinal)
                && childCount != 1)
            {
                return new Failure("A 'not' group must have exactly one child.", null);
            }
        }

        if (nodes.Count > SegmentLimits.MaxChildrenPerGroup
            && childrenByParent.TryGetValue(Guid.Empty, out var roots)
            && roots.Count > SegmentLimits.MaxChildrenPerGroup)
        {
            return new Failure(
                $"The criteria root may hold at most {SegmentLimits.MaxChildrenPerGroup} children.", null);
        }

        return ValidateDepthAndCycles(nodes, byId);
    }

    private static Failure? ValidateDepthAndCycles(
        IReadOnlyList<SegmentCriteriaNode> nodes, IReadOnlyDictionary<Guid, SegmentCriteriaNode> byId)
    {
        foreach (var node in nodes)
        {
            var depth = 1;
            var seen = new HashSet<Guid> { node.NodeId };
            var cursor = node.ParentNodeId;
            while (cursor is { } parentId)
            {
                if (!seen.Add(parentId))
                {
                    return new Failure("The criteria tree contains a ParentNodeId cycle.", null);
                }

                depth++;
                if (depth > SegmentLimits.MaxCriteriaDepth)
                {
                    return new Failure(
                        $"The criteria tree may be at most {SegmentLimits.MaxCriteriaDepth} levels deep.", null);
                }

                cursor = byId.TryGetValue(parentId, out var parent) ? parent.ParentNodeId : null;
            }
        }

        return null;
    }

    private static Failure? ValidateNode(SegmentCriteriaNode node, string subjectType)
    {
        if (!SegmentCriteriaNodeKinds.IsValid(node.NodeKind))
        {
            return new Failure(
                $"NodeKind must be one of: {string.Join(", ", SegmentCriteriaNodeKinds.All)}.", null);
        }

        if (node.IsGroup())
        {
            if (!SegmentGroupOperators.IsValid(node.GroupOperator))
            {
                return new Failure(
                    $"A group node needs a GroupOperator ({string.Join(", ", SegmentGroupOperators.All)}).", null);
            }

            return node.AttributeCode is not null || node.Operator is not null || node.Values.Count > 0
                ? new Failure("A group node carries no AttributeCode, Operator or Values.", null)
                : null;
        }

        // ---- predicate ----
        if (node.GroupOperator is not null)
        {
            return new Failure("A predicate node carries no GroupOperator.", null);
        }

        var definition = SegmentAttributeCatalog.Find(node.AttributeCode);
        if (definition is null)
        {
            return new Failure(
                $"AttributeCode '{node.AttributeCode}' is not declared in the segment attribute catalog.",
                SegmentErrorCodes.AttributeUnknown);
        }

        if (!definition.AppliesToSubjectType(subjectType))
        {
            return new Failure(
                $"Attribute '{definition.AttributeCode}' cannot be used in a '{subjectType}' segment "
                + $"(it applies to: {string.Join(", ", definition.AllowedSubjectTypes)}).",
                SegmentErrorCodes.AttributeNotApplicableForSubjectType);
        }

        if (!SegmentOperators.IsValid(node.Operator) || !definition.SupportsOperator(node.Operator))
        {
            return new Failure(
                $"Operator '{node.Operator}' is not supported for attribute '{definition.AttributeCode}' "
                + $"(supported: {string.Join(", ", definition.Operators)}).",
                SegmentErrorCodes.OperatorNotSupported);
        }

        if (!SegmentValueTypes.IsValid(node.ValueType)
            || !string.Equals(SegmentValueTypes.Normalize(node.ValueType), definition.ValueType,
                StringComparison.Ordinal))
        {
            return new Failure(
                $"ValueType for attribute '{definition.AttributeCode}' must be '{definition.ValueType}'.", null);
        }

        var arityFailure = ValidateValues(node, definition);
        if (arityFailure is not null)
        {
            return arityFailure;
        }

        foreach (var required in definition.RequiredParameters)
        {
            if (!node.Parameters.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return new Failure(
                    $"Attribute '{definition.AttributeCode}' requires the parameter '{required}'.",
                    SegmentErrorCodes.AttributeParameterMissing);
            }
        }

        return ValidateConceptAffinityParameters(node, definition);
    }

    private static Failure? ValidateValues(SegmentCriteriaNode node, SegmentAttributeDefinition definition)
    {
        var op = SegmentOperators.Normalize(node.Operator);
        var (min, max) = SegmentOperators.Arity(op);
        if (node.Values.Count < min || node.Values.Count > max)
        {
            return new Failure(
                $"Operator '{op}' takes between {min} and {max} value(s); {node.Values.Count} were supplied.", null);
        }

        foreach (var raw in node.Values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Failure("A criteria value may not be blank.", null);
            }

            if (!TryParseValue(raw, definition.ValueType, out _))
            {
                return new Failure(
                    $"Value '{raw}' is not a valid {definition.ValueType} for attribute "
                    + $"'{definition.AttributeCode}'.", null);
            }
        }

        if (string.Equals(op, SegmentOperators.Between, StringComparison.Ordinal)
            && CompareValues(node.Values[0], node.Values[1], definition.ValueType) >= 0)
        {
            return new Failure("A 'between' predicate needs its lower bound first.", null);
        }

        return null;
    }

    private static Failure? ValidateConceptAffinityParameters(
        SegmentCriteriaNode node, SegmentAttributeDefinition definition)
    {
        if (!string.Equals(definition.AttributeCode, SegmentAttributeCatalog.ConceptAffinity, StringComparison.Ordinal))
        {
            return null;
        }

        if (node.Parameters.TryGetValue(SegmentAttributeCatalog.ParameterMaxDepth, out var depthRaw)
            && !string.IsNullOrWhiteSpace(depthRaw))
        {
            if (!int.TryParse(depthRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var depth)
                || depth < 1
                || depth > SegmentLimits.MaxConceptAffinityDepth)
            {
                return new Failure(
                    $"concept.affinity maxDepth must be 1 or {SegmentLimits.MaxConceptAffinityDepth}; "
                    + "there is no transitive closure.",
                    SegmentErrorCodes.ConceptDepthExceeded);
            }
        }

        if (node.Parameters.TryGetValue(SegmentAttributeCatalog.ParameterSubjectId, out var subjectRaw)
            && !string.IsNullOrWhiteSpace(subjectRaw)
            && !Guid.TryParse(subjectRaw, out _))
        {
            return new Failure("concept.affinity subjectId must be a valid Guid.", null);
        }

        return null;
    }

    /// <summary>Resolves the bounded traversal depth for one <c>concept.affinity</c> predicate. Defaults to 1 and never
    /// exceeds the ceiling — the validator already rejected anything larger, so this can only clamp.</summary>
    public static int ResolveConceptAffinityDepth(IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue(SegmentAttributeCatalog.ParameterMaxDepth, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var depth))
        {
            return Math.Clamp(depth, 1, SegmentLimits.MaxConceptAffinityDepth);
        }

        return SegmentLimits.DefaultConceptAffinityDepth;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Value parsing / comparison (shared by the validator and the evaluator, so authoring and evaluation agree)
    // ---------------------------------------------------------------------------------------------------------

    public static bool TryParseValue(string? raw, string valueType, out object? parsed)
    {
        parsed = null;
        if (raw is null)
        {
            return false;
        }

        var text = raw.Trim();
        switch (SegmentValueTypes.Normalize(valueType))
        {
            case SegmentValueTypes.Number:
                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                {
                    parsed = number;
                    return true;
                }

                return false;

            case SegmentValueTypes.Date:
                if (DateTimeOffset.TryParse(
                        text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                {
                    parsed = date;
                    return true;
                }

                return false;

            case SegmentValueTypes.Bool:
                if (bool.TryParse(text, out var flag))
                {
                    parsed = flag;
                    return true;
                }

                return false;

            case SegmentValueTypes.Guid:
                if (Guid.TryParse(text, out var guid))
                {
                    parsed = guid;
                    return true;
                }

                return false;

            default:
                parsed = text;
                return true;
        }
    }

    public static int CompareValues(string? left, string? right, string valueType)
    {
        if (!TryParseValue(left, valueType, out var l) || !TryParseValue(right, valueType, out var r))
        {
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        return (l, r) switch
        {
            (decimal a, decimal b) => a.CompareTo(b),
            (DateTimeOffset a, DateTimeOffset b) => a.CompareTo(b),
            (bool a, bool b) => a.CompareTo(b),
            (Guid a, Guid b) => a.CompareTo(b),
            (string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase),
            _ => 0
        };
    }

    // ---------------------------------------------------------------------------------------------------------
    // TargetCustomer (manual membership only)
    // ---------------------------------------------------------------------------------------------------------

    public static Failure? ValidateTargetCustomer(
        Segment segment, string? subjectType, Guid subjectId, string? membershipMode, string? selectionReason,
        IReadOnlyList<string>? reasonCodes, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        if (string.Equals(SegmentTypes.Normalize(segment.SegmentType), SegmentTypes.Dynamic, StringComparison.Ordinal))
        {
            return new Failure(
                "A dynamic segment accepts no manual membership row; switch it to hybrid if a manual exception is "
                + "needed, so the label never lies about where a member came from.",
                SegmentErrorCodes.TypeForbidsManualMembership);
        }

        if (!SegmentSubjectTypes.IsValid(subjectType))
        {
            return new Failure(
                $"SubjectType must be one of: {string.Join(", ", SegmentSubjectTypes.All)}.", null);
        }

        if (!string.Equals(SegmentSubjectTypes.Normalize(subjectType), segment.SubjectType, StringComparison.Ordinal))
        {
            return new Failure(
                $"SubjectType must match the segment SubjectType ('{segment.SubjectType}').",
                SegmentErrorCodes.SubjectTypeMismatch);
        }

        if (subjectId == Guid.Empty)
        {
            return new Failure("SubjectId is required.", null);
        }

        if (!SegmentMembershipModes.IsValid(membershipMode))
        {
            return new Failure(
                $"MembershipMode must be one of: {string.Join(", ", SegmentMembershipModes.All)}.", null);
        }

        var reason = Trim(selectionReason);
        if (reason is null)
        {
            return new Failure("SelectionReason is required: a manual membership without a reason is not authorable.", null);
        }

        if (reason.Length > SegmentLimits.MaxSelectionReasonLength)
        {
            return new Failure(
                $"SelectionReason must be at most {SegmentLimits.MaxSelectionReasonLength} characters.", null);
        }

        if (reasonCodes is null || reasonCodes.Count == 0)
        {
            return new Failure("At least one ReasonCode is required.", null);
        }

        foreach (var code in reasonCodes)
        {
            if (!SegmentReasonCodes.IsValid(code))
            {
                return new Failure($"ReasonCode '{code}' is not a declared segment reason code.", null);
            }
        }

        return ValidateEffectiveRange(effectiveFrom, effectiveTo);
    }
}
