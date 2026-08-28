using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — the criteria tree is validated at AUTHORING time, not at evaluation time. That is the whole point of
/// a stored predicate tree over a query DSL: a broken rule is refused when it is written, not discovered later as a
/// segment that quietly matches nobody.
/// </summary>
public sealed class SegmentCriteriaValidationTests
{
    private const string Contact = SegmentSubjectTypes.Contact;
    private const string Account = SegmentSubjectTypes.Account;

    private static SegmentValidation.Failure? Validate(
        params SegmentCriteriaNodeInput[] nodes)
        => SegmentValidation.ValidateCriteria(
            SegmentTypes.Dynamic, Contact, SegmentMapper.ToCriteria(nodes));

    [Fact]
    public void An_undeclared_attribute_is_refused_with_its_own_code()
    {
        var failure = Validate(SegmentTestBuilders.Predicate(
            "contact.favourite-colour", SegmentOperators.Eq, SegmentValueTypes.String, new[] { "blue" }));

        Assert.NotNull(failure);
        Assert.Equal(SegmentErrorCodes.AttributeUnknown, failure!.Code);
    }

    [Fact]
    public void An_operator_the_catalog_does_not_allow_for_that_attribute_is_refused()
    {
        var failure = Validate(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ContactIsPrimary, SegmentOperators.Between, SegmentValueTypes.Bool,
            new[] { "true", "false" }));

        Assert.NotNull(failure);
        Assert.Equal(SegmentErrorCodes.OperatorNotSupported, failure!.Code);
    }

    [Fact]
    public void A_value_type_that_disagrees_with_the_catalog_is_refused()
    {
        var failure = Validate(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.Number,
            new[] { "12" }));

        Assert.NotNull(failure);
    }

    [Theory]
    [InlineData(SegmentOperators.Eq, 0)]
    [InlineData(SegmentOperators.Eq, 2)]
    [InlineData(SegmentOperators.Between, 1)]
    [InlineData(SegmentOperators.IsNull, 1)]
    public void Operator_arity_is_enforced(string op, int valueCount)
    {
        var values = Enumerable.Range(0, valueCount).Select(i => $"v{i}").ToArray();
        var failure = Validate(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ContactSpecialty, op, SegmentValueTypes.String, values));

        Assert.NotNull(failure);
    }

    [Fact]
    public void A_required_parameter_is_enforced_with_its_own_code()
    {
        var missingChannel = Validate(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ConsentEligibility, SegmentOperators.Eq, SegmentValueTypes.String,
            new[] { "allowed" }));

        Assert.NotNull(missingChannel);
        Assert.Equal(SegmentErrorCodes.AttributeParameterMissing, missingChannel!.Code);

        var complete = Validate(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ConsentEligibility, SegmentOperators.Eq, SegmentValueTypes.String,
            new[] { "allowed" },
            parameters: new Dictionary<string, string> { ["channel"] = "email", ["purpose"] = "marketing" }));

        Assert.Null(complete);
    }

    [Fact]
    public void The_in_operator_is_capped_and_the_cap_is_a_refusal_not_a_trim()
    {
        var tooMany = Enumerable.Range(0, SegmentLimits.MaxValuesPerInOperator + 1)
            .Select(i => $"v{i}").ToArray();

        var failure = Validate(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.In, SegmentValueTypes.String, tooMany));

        Assert.NotNull(failure);
    }

    [Fact]
    public void The_node_count_ceiling_is_enforced()
    {
        var nodes = Enumerable.Range(0, SegmentLimits.MaxCriteriaNodes + 1)
            .Select(i => SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "cardiology" }, sortOrder: i))
            .ToArray();

        Assert.NotNull(Validate(nodes));
    }

    [Fact]
    public void The_depth_ceiling_is_enforced()
    {
        var nodes = new List<SegmentCriteriaNodeInput>();
        Guid? parent = null;
        for (var depth = 0; depth < SegmentLimits.MaxCriteriaDepth; depth++)
        {
            var groupId = Guid.NewGuid();
            nodes.Add(SegmentTestBuilders.Group(SegmentGroupOperators.And, groupId, parent));
            parent = groupId;
        }

        // One predicate below the deepest group takes the tree past the ceiling.
        nodes.Add(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
            new[] { "cardiology" }, parentNodeId: parent));

        Assert.NotNull(Validate(nodes.ToArray()));
    }

    [Fact]
    public void A_group_may_not_hold_more_children_than_the_ceiling_allows()
    {
        var groupId = Guid.NewGuid();
        var nodes = new List<SegmentCriteriaNodeInput>
        {
            SegmentTestBuilders.Group(SegmentGroupOperators.Or, groupId)
        };

        for (var i = 0; i <= SegmentLimits.MaxChildrenPerGroup; i++)
        {
            nodes.Add(SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { $"s{i}" }, parentNodeId: groupId, sortOrder: i));
        }

        Assert.NotNull(Validate(nodes.ToArray()));
    }

    [Fact]
    public void A_group_needs_a_child_and_a_not_group_needs_exactly_one()
    {
        var emptyGroupId = Guid.NewGuid();
        Assert.NotNull(Validate(
            SegmentTestBuilders.Group(SegmentGroupOperators.And, emptyGroupId),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "cardiology" }, sortOrder: 1)));

        var notGroupId = Guid.NewGuid();
        Assert.NotNull(Validate(
            SegmentTestBuilders.Group(SegmentGroupOperators.Not, notGroupId),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "a" }, parentNodeId: notGroupId, sortOrder: 0),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "b" }, parentNodeId: notGroupId, sortOrder: 1)));
    }

    [Fact]
    public void Sibling_sort_order_must_be_unique_because_determinism_depends_on_it()
    {
        Assert.NotNull(Validate(
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "a" }, sortOrder: 1),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "b" }, sortOrder: 1)));
    }

    [Fact]
    public void A_parent_cycle_is_refused()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        // Built directly (not through the mapper, which reassigns ids) so the cycle survives into the validator.
        var nodes = new List<SegmentCriteriaNode>
        {
            new()
            {
                NodeId = first, ParentNodeId = second, NodeKind = SegmentCriteriaNodeKinds.Group,
                GroupOperator = SegmentGroupOperators.And, SortOrder = 0
            },
            new()
            {
                NodeId = second, ParentNodeId = first, NodeKind = SegmentCriteriaNodeKinds.Group,
                GroupOperator = SegmentGroupOperators.And, SortOrder = 0
            },
            new()
            {
                NodeId = Guid.NewGuid(), ParentNodeId = first, NodeKind = SegmentCriteriaNodeKinds.Predicate,
                AttributeCode = SegmentAttributeCatalog.ContactSpecialty, Operator = SegmentOperators.Eq,
                ValueType = SegmentValueTypes.String, Values = new List<string> { "cardiology" }, SortOrder = 1
            }
        };

        Assert.NotNull(SegmentValidation.ValidateCriteria(SegmentTypes.Dynamic, Contact, nodes));
    }

    [Fact]
    public void An_attribute_that_does_not_apply_to_the_subject_type_is_refused_with_its_own_code()
    {
        var failure = SegmentValidation.ValidateCriteria(
            SegmentTypes.Dynamic, Account,
            SegmentMapper.ToCriteria(new[]
            {
                SegmentTestBuilders.Predicate(
                    SegmentAttributeCatalog.ConceptAffinity, SegmentOperators.Eq, SegmentValueTypes.Guid,
                    new[] { Guid.NewGuid().ToString() })
            }));

        Assert.NotNull(failure);
        Assert.Equal(SegmentErrorCodes.AttributeNotApplicableForSubjectType, failure!.Code);
    }

    [Theory]
    [InlineData("1", null)]
    [InlineData("2", null)]
    [InlineData("3", SegmentErrorCodes.ConceptDepthExceeded)]
    [InlineData("0", SegmentErrorCodes.ConceptDepthExceeded)]
    [InlineData("deep", SegmentErrorCodes.ConceptDepthExceeded)]
    public void Concept_affinity_depth_is_bounded_at_two(string depth, string? expectedCode)
    {
        var failure = Validate(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ConceptAffinity, SegmentOperators.Eq, SegmentValueTypes.Guid,
            new[] { Guid.NewGuid().ToString() },
            parameters: new Dictionary<string, string> { ["maxDepth"] = depth }));

        Assert.Equal(expectedCode, failure?.Code);
    }

    [Fact]
    public void A_between_predicate_needs_its_lower_bound_first()
    {
        var failure = SegmentValidation.ValidateCriteria(
            SegmentTypes.Dynamic, Contact,
            SegmentMapper.ToCriteria(new[]
            {
                SegmentTestBuilders.Predicate(
                    SegmentAttributeCatalog.ContactCreatedAt, SegmentOperators.Between, SegmentValueTypes.Date,
                    new[] { "2026-01-01T00:00:00Z", "2020-01-01T00:00:00Z" })
            }));

        Assert.NotNull(failure);
    }

    [Fact]
    public void A_static_segment_may_not_carry_criteria_and_a_dynamic_one_must()
    {
        var staticWithRule = SegmentValidation.ValidateCriteria(
            SegmentTypes.Static, Contact,
            SegmentMapper.ToCriteria(SegmentTestBuilders.SpecialtyIs("cardiology").ToArray()));
        Assert.NotNull(staticWithRule);

        var dynamicWithout = SegmentValidation.ValidateCriteria(
            SegmentTypes.Dynamic, Contact, new List<SegmentCriteriaNode>());
        Assert.NotNull(dynamicWithout);
    }

    [Fact]
    public void The_mapper_always_assigns_fresh_node_ids_so_a_caller_cannot_smuggle_one_in()
    {
        var forged = Guid.NewGuid();
        var mapped = SegmentMapper.ToCriteria(new[]
        {
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "cardiology" }, nodeId: forged)
        });

        Assert.NotEqual(forged, mapped.Single().NodeId);
    }
}
