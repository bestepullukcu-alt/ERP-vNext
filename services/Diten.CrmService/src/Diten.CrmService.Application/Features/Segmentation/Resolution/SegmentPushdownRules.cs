using System.Text.RegularExpressions;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// WP-E2E-FIX-3 (E1-B2) — the ONE place that says which part of a segment rule may narrow the candidate query. Both the
/// application pre-filter (<see cref="SegmentCandidatePrefilter"/>) and the Mongo translation in the candidate source read
/// these rules, so "what is pushed" can never drift between the two.
/// <para><b>Narrowing only.</b> A pushed condition can only ever shrink the candidate SUPERSET; the in-memory evaluator
/// still decides every member exactly. So a leaf is pushable only where "satisfied" implies "inside the pushed set":
/// a positive operator (eq / in / contains — the evaluator's "any value matches"), never under a negation (a NOT group,
/// a <c>Negate</c> flag, ne / not-in / is-null), never a date (stored as a BSON array). An OR group narrows only when
/// EVERY branch narrows; an AND group keeps the narrowing branches and drops the rest.</para>
/// </summary>
public static class SegmentPushdownRules
{
    /// <summary>An id pre-query whose answer is larger than this is not pushed (the query would carry a huge
    /// <c>$in</c> and win nothing); the leaf then falls back to today's path (no narrowing).</summary>
    public const int MaxPrefilterIds = 50_000;

    public enum LeafKind
    {
        /// <summary>Not pushable: contributes "true" (no narrowing).</summary>
        None,

        /// <summary>A field of the subject document itself (one native Mongo predicate).</summary>
        Native,

        /// <summary>Answered by an id pre-query (territory coverage / account–contact link) → <c>_id IN</c>.</summary>
        Prefilter
    }

    /// <summary>The subject-document field of a native attribute, or null.</summary>
    public static string? NativeField(string? attributeCode, bool isContact) =>
        (attributeCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            SegmentAttributeCatalog.AccountType when !isContact => "AccountType",
            SegmentAttributeCatalog.AccountCategory when !isContact => "AccountCategory",
            SegmentAttributeCatalog.AccountStatus when !isContact => "Status",
            SegmentAttributeCatalog.AccountCountry when !isContact => "CountryRef",
            SegmentAttributeCatalog.AccountCity when !isContact => "CityRef",
            SegmentAttributeCatalog.AccountDistrict when !isContact => "DistrictRef",
            SegmentAttributeCatalog.AccountParentAccount when !isContact => "ParentAccountId",

            SegmentAttributeCatalog.ContactType when isContact => "ContactType",
            SegmentAttributeCatalog.ContactStatus when isContact => "Status",
            SegmentAttributeCatalog.ContactGender when isContact => "Gender",
            SegmentAttributeCatalog.ContactSpecialty when isContact => "Specialty",
            SegmentAttributeCatalog.ContactProfessionalTitle when isContact => "ProfessionalTitle",
            SegmentAttributeCatalog.ContactDepartment when isContact => "Department",
            SegmentAttributeCatalog.ContactCountry when isContact => "CountryRef",
            SegmentAttributeCatalog.ContactCity when isContact => "CityRef",
            SegmentAttributeCatalog.ContactDistrict when isContact => "DistrictRef",
            SegmentAttributeCatalog.ContactPreferredLanguage when isContact => "PreferredLanguage",

            // account.attribute lives in another collection; the join is answered in Phase 2, not guessed at here.
            _ => null
        };

    /// <summary>
    /// A string criterion as a (case-insensitive, option <c>i</c>) regex body: always escaped (a value is literal text),
    /// <c>^…$</c> for eq / in, unanchored for contains. <paramref name="turkishInsensitive"/> (the candidate superset —
    /// WP-VP-FIX-2 follow-up) also folds i / ı / I / İ into one class, so it can only ever be WIDER than the evaluator's
    /// OrdinalIgnoreCase; the fully-native count uses the plain form.
    /// </summary>
    public static string StringPattern(string value, bool anchored, bool turkishInsensitive)
    {
        var body = turkishInsensitive ? TurkishInsensitivePattern.Build(value) : Regex.Escape(value);
        return anchored ? $"^{body}$" : body;
    }

    /// <summary>The trimmed, non-blank values of a predicate.</summary>
    public static IReadOnlyList<string> Values(SegmentCriteriaNode node)
        => node.Values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();

    public static LeafKind Classify(SegmentCriteriaNode node, bool isContact)
    {
        if (node.Negate || node.IsGroup() || Values(node).Count == 0)
        {
            return LeafKind.None;
        }

        var definition = SegmentAttributeCatalog.Find(node.AttributeCode);
        if (definition is null || string.Equals(definition.ValueType, SegmentValueTypes.Date, StringComparison.Ordinal))
        {
            return LeafKind.None;
        }

        var op = SegmentOperators.Normalize(node.Operator);
        var code = (node.AttributeCode ?? string.Empty).Trim().ToLowerInvariant();

        if (NativeField(code, isContact) is not null)
        {
            return op is SegmentOperators.Eq or SegmentOperators.In or SegmentOperators.Contains
                ? LeafKind.Native
                : LeafKind.None;
        }

        return code switch
        {
            SegmentAttributeCatalog.TerritoryNode or SegmentAttributeCatalog.TerritoryModel
                when op is SegmentOperators.Eq or SegmentOperators.In => LeafKind.Prefilter,
            // Only the positive answer: "has no coverage" is a negation in disguise.
            SegmentAttributeCatalog.TerritoryHasCoverage
                when op == SegmentOperators.Eq && IsTrue(Values(node)[0]) => LeafKind.Prefilter,
            SegmentAttributeCatalog.ContactAccountRole or SegmentAttributeCatalog.ContactAccountType
                when isContact && op is SegmentOperators.Eq or SegmentOperators.In or SegmentOperators.Contains
                => LeafKind.Prefilter,
            SegmentAttributeCatalog.ContactIsPrimary
                when isContact && op == SegmentOperators.Eq && bool.TryParse(Values(node)[0], out _) => LeafKind.Prefilter,
            _ => LeafKind.None
        };
    }

    /// <summary>The pre-filter leaves worth querying: those on a path that can actually narrow (an OR group only when
    /// every branch narrows). A pre-query whose answer could not be used is never issued.</summary>
    public static IReadOnlyList<SegmentCriteriaNode> PrefilterLeaves(
        IReadOnlyList<SegmentCriteriaNode> criteria, string matchMode, bool isContact)
    {
        var result = new List<SegmentCriteriaNode>();
        if (Roots(criteria, out var tree) is not { Count: > 0 } roots)
        {
            return result;
        }

        var isAny = string.Equals(SegmentMatchModes.Normalize(matchMode), SegmentMatchModes.Any, StringComparison.Ordinal);
        Collect(roots, isAny, tree, isContact, result);
        return result;
    }

    /// <summary>True when EVERY condition of the rule is a native, positive, non-date predicate under AND / OR groups —
    /// then the native Mongo filter IS the rule and the store can count it (the preview's over-the-ceiling count).</summary>
    public static bool IsFullyNative(IReadOnlyList<SegmentCriteriaNode> criteria, bool isContact)
    {
        if (Roots(criteria, out var tree) is not { Count: > 0 } roots)
        {
            return false;
        }

        return roots.All(r => IsNativeTree(r, tree, isContact));
    }

    private static bool IsNativeTree(
        SegmentCriteriaNode node, IReadOnlyDictionary<Guid, IReadOnlyList<SegmentCriteriaNode>> tree, bool isContact)
    {
        if (!node.IsGroup())
        {
            return Classify(node, isContact) == LeafKind.Native;
        }

        if (node.Negate || IsNotGroup(node) || !tree.TryGetValue(node.NodeId, out var kids) || kids.Count == 0)
        {
            return false;
        }

        return kids.All(k => IsNativeTree(k, tree, isContact));
    }

    /// <summary>Can this node narrow at all (statically)? Mirrors the translation's AND / OR combination.</summary>
    public static bool CanNarrow(
        SegmentCriteriaNode node, IReadOnlyDictionary<Guid, IReadOnlyList<SegmentCriteriaNode>> tree, bool isContact)
    {
        if (!node.IsGroup())
        {
            return Classify(node, isContact) != LeafKind.None;
        }

        if (node.Negate || IsNotGroup(node) || !tree.TryGetValue(node.NodeId, out var kids) || kids.Count == 0)
        {
            return false;
        }

        return IsOrGroup(node)
            ? kids.All(k => CanNarrow(k, tree, isContact))
            : kids.Any(k => CanNarrow(k, tree, isContact));
    }

    private static void Collect(
        IReadOnlyList<SegmentCriteriaNode> siblings, bool isOr,
        IReadOnlyDictionary<Guid, IReadOnlyList<SegmentCriteriaNode>> tree, bool isContact, List<SegmentCriteriaNode> into)
    {
        if (isOr && !siblings.All(s => CanNarrow(s, tree, isContact)))
        {
            return; // one wide branch makes the whole OR wide: nothing below can narrow
        }

        foreach (var node in siblings)
        {
            if (!node.IsGroup())
            {
                if (Classify(node, isContact) == LeafKind.Prefilter)
                {
                    into.Add(node);
                }

                continue;
            }

            if (node.Negate || IsNotGroup(node) || !tree.TryGetValue(node.NodeId, out var kids) || kids.Count == 0)
            {
                continue;
            }

            Collect(kids, IsOrGroup(node), tree, isContact, into);
        }
    }

    /// <summary>The root predicates / groups, plus the parent → children map (ordered by SortOrder).</summary>
    public static IReadOnlyList<SegmentCriteriaNode>? Roots(
        IReadOnlyList<SegmentCriteriaNode> criteria,
        out IReadOnlyDictionary<Guid, IReadOnlyList<SegmentCriteriaNode>> tree)
    {
        // Guid.Empty stands for "child of the implicit root": a Dictionary cannot take a null key.
        tree = criteria.GroupBy(n => n.ParentNodeId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SegmentCriteriaNode>)g.OrderBy(n => n.SortOrder).ToList());
        return tree.TryGetValue(Guid.Empty, out var roots) ? roots : null;
    }

    public static bool IsOrGroup(SegmentCriteriaNode node)
        => string.Equals(SegmentGroupOperators.Normalize(node.GroupOperator), SegmentGroupOperators.Or, StringComparison.Ordinal);

    public static bool IsNotGroup(SegmentCriteriaNode node)
        => string.Equals(SegmentGroupOperators.Normalize(node.GroupOperator), SegmentGroupOperators.Not, StringComparison.Ordinal);

    private static bool IsTrue(string value) => bool.TryParse(value, out var b) && b;
}
