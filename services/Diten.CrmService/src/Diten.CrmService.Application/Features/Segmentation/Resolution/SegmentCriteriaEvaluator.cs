using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 criteria evaluator: a PURE function of (criteria tree, resolved attribute values). It performs no I/O
/// and no writes, so it is unit-testable in isolation and no evaluation path can ever mutate anything.
/// <para><b>Three-valued on purpose.</b> A node answers true, false or UNRESOLVED. Unresolved dominates both AND and
/// OR: if any part of the rule could not be answered for this candidate, the candidate is eliminated fail-closed and
/// carries the SPECIFIC reason (consent_unknown, territory_coverage_unavailable, concept_product_node_missing, ...)
/// rather than a generic "did not match". A candidate is never admitted on an unanswered question.</para>
/// <para>Multi-valued attributes (a contact linked to several accounts, an account covered by several nodes) are
/// evaluated existentially: eq/in/contains/comparisons match when ANY value satisfies them, and ne/not-in match when NO
/// value does — which is also the answer when there is no value at all.</para>
/// </summary>
public static class SegmentCriteriaEvaluator
{
    /// <summary>The verdict for one node or for the whole tree. <c>Matched</c> is null when the answer is UNRESOLVED.
    /// Nested so this file declares a single top-level public type.</summary>
    public sealed record Outcome(bool? Matched, IReadOnlyList<string> ReasonCodes)
    {
        public static Outcome True(params string[] reasons) => new(true, reasons);
        public static Outcome False(params string[] reasons) => new(false, reasons);
        public static Outcome Unresolved(params string[] reasons) => new(null, reasons);
    }

    /// <summary>Evaluates a whole segment rule for one candidate. A segment with no criteria (a static one) is never
    /// passed here — the resolver does not run the engine at all in that case.</summary>
    public static Outcome Evaluate(Segment segment, SegmentAttributeValueSet values)
    {
        // Guid.Empty stands for "child of the implicit root": a Dictionary cannot take a null key, and a real NodeId
        // is never Guid.Empty (the validator rejects that).
        var childrenByParent = segment.Criteria
            .GroupBy(n => n.ParentNodeId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SegmentCriteriaNode>)g.OrderBy(n => n.SortOrder).ToList());

        var roots = childrenByParent.TryGetValue(Guid.Empty, out var r) ? r : Array.Empty<SegmentCriteriaNode>();
        if (roots.Count == 0)
        {
            return Outcome.False(SegmentReasonCodes.CriteriaNotMatched);
        }

        var outcomes = roots.Select(node => EvaluateNode(node, childrenByParent, values)).ToList();
        var combined = Combine(
            outcomes,
            string.Equals(SegmentMatchModes.Normalize(segment.MatchMode), SegmentMatchModes.Any, StringComparison.Ordinal)
                ? SegmentGroupOperators.Or
                : SegmentGroupOperators.And);

        return Finalize(combined);
    }

    private static Outcome Finalize(Outcome outcome) => outcome.Matched switch
    {
        true => new Outcome(true, Distinct(outcome.ReasonCodes.Append(SegmentReasonCodes.CriteriaMatched))),
        false => new Outcome(false, Distinct(outcome.ReasonCodes.Append(SegmentReasonCodes.CriteriaNotMatched))),
        _ => new Outcome(null, Distinct(outcome.ReasonCodes))
    };

    private static Outcome EvaluateNode(
        SegmentCriteriaNode node,
        IReadOnlyDictionary<Guid, IReadOnlyList<SegmentCriteriaNode>> childrenByParent,
        SegmentAttributeValueSet values)
    {
        var outcome = node.IsGroup()
            ? EvaluateGroup(node, childrenByParent, values)
            : EvaluatePredicate(node, values);

        // Node-level NOT. An unresolved answer stays unresolved: negating "we do not know" does not produce knowledge.
        return node.Negate && outcome.Matched is { } matched
            ? outcome with { Matched = !matched }
            : outcome;
    }

    private static Outcome EvaluateGroup(
        SegmentCriteriaNode node,
        IReadOnlyDictionary<Guid, IReadOnlyList<SegmentCriteriaNode>> childrenByParent,
        SegmentAttributeValueSet values)
    {
        var children = childrenByParent.TryGetValue(node.NodeId, out var kids)
            ? kids
            : Array.Empty<SegmentCriteriaNode>();

        if (children.Count == 0)
        {
            // The validator rejects an empty group at authoring time; evaluating one is treated as "no evidence".
            return Outcome.Unresolved(SegmentReasonCodes.AttributeNotResolvable);
        }

        var op = SegmentGroupOperators.Normalize(node.GroupOperator);
        var outcomes = children.Select(child => EvaluateNode(child, childrenByParent, values)).ToList();

        if (string.Equals(op, SegmentGroupOperators.Not, StringComparison.Ordinal))
        {
            var single = outcomes[0];
            return single.Matched is { } matched ? single with { Matched = !matched } : single;
        }

        return Combine(outcomes, op);
    }

    private static Outcome Combine(IReadOnlyList<Outcome> outcomes, string op)
    {
        // Fail-closed: an unanswered question sinks the whole combination, and its reason travels up so the
        // elimination is explained by its real cause.
        var unresolved = outcomes.Where(o => o.Matched is null).ToList();
        if (unresolved.Count > 0)
        {
            return new Outcome(null, Distinct(unresolved.SelectMany(o => o.ReasonCodes)));
        }

        var isOr = string.Equals(op, SegmentGroupOperators.Or, StringComparison.Ordinal);
        var matched = isOr ? outcomes.Any(o => o.Matched == true) : outcomes.All(o => o.Matched == true);

        // Only the reasons that explain the OUTCOME are kept: on a failure the failing branches, on a success nothing
        // extra (a match is explained by criteria_matched alone).
        var reasons = matched
            ? Array.Empty<string>()
            : Distinct(outcomes.Where(o => o.Matched == false).SelectMany(o => o.ReasonCodes));

        return new Outcome(matched, reasons);
    }

    private static Outcome EvaluatePredicate(SegmentCriteriaNode node, SegmentAttributeValueSet values)
    {
        if (values.TryGetUnresolved(node.NodeId, out var unresolvedReason))
        {
            return Outcome.Unresolved(unresolvedReason);
        }

        var definition = SegmentAttributeCatalog.Find(node.AttributeCode);
        if (definition is null)
        {
            // Unreachable through the write path (the catalog is enforced at authoring time); still fail-closed.
            return Outcome.Unresolved(SegmentReasonCodes.AttributeNotResolvable);
        }

        var actual = values.GetValues(node.NodeId);
        var matched = Matches(node, definition.ValueType, actual);
        if (matched)
        {
            return Outcome.True();
        }

        var advisory = values.GetAdvisory(node.NodeId);
        return advisory is null ? Outcome.False() : Outcome.False(advisory);
    }

    private static bool Matches(SegmentCriteriaNode node, string valueType, IReadOnlyList<string?> actual)
    {
        var op = SegmentOperators.Normalize(node.Operator);
        var present = actual.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList();

        switch (op)
        {
            case SegmentOperators.IsNull:
                return present.Count == 0;

            case SegmentOperators.IsNotNull:
                return present.Count > 0;

            case SegmentOperators.Eq:
                return present.Any(v => Equal(v, node.Values.ElementAtOrDefault(0), valueType));

            case SegmentOperators.Ne:
                // No value at all also satisfies "not equal to x": nothing here equals it.
                return !present.Any(v => Equal(v, node.Values.ElementAtOrDefault(0), valueType));

            case SegmentOperators.In:
                return present.Any(v => node.Values.Any(expected => Equal(v, expected, valueType)));

            case SegmentOperators.NotIn:
                return !present.Any(v => node.Values.Any(expected => Equal(v, expected, valueType)));

            case SegmentOperators.Contains:
                var needle = node.Values.ElementAtOrDefault(0);
                return needle is not null
                       && present.Any(v => v.Contains(needle.Trim(), StringComparison.OrdinalIgnoreCase));

            case SegmentOperators.Gt:
                return present.Any(v => SegmentValidation.CompareValues(v, node.Values[0], valueType) > 0);

            case SegmentOperators.Gte:
                return present.Any(v => SegmentValidation.CompareValues(v, node.Values[0], valueType) >= 0);

            case SegmentOperators.Lt:
                return present.Any(v => SegmentValidation.CompareValues(v, node.Values[0], valueType) < 0);

            case SegmentOperators.Lte:
                return present.Any(v => SegmentValidation.CompareValues(v, node.Values[0], valueType) <= 0);

            case SegmentOperators.Between:
                return present.Any(v =>
                    SegmentValidation.CompareValues(v, node.Values[0], valueType) >= 0
                    && SegmentValidation.CompareValues(v, node.Values[1], valueType) <= 0);

            default:
                return false;
        }
    }

    private static bool Equal(string? left, string? right, string valueType)
        => right is not null && SegmentValidation.CompareValues(left, right, valueType) == 0;

    private static IReadOnlyList<string> Distinct(IEnumerable<string> reasons)
        => reasons.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal).ToList();
}
