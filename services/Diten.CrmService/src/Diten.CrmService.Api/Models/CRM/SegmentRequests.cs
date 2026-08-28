using Diten.CrmService.Application.Features.Segmentation;

namespace Diten.CrmService.Api.Models.CRM;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0167 FU02 request bodies. TenantId appears in NONE of them: it is resolved server-side from the claim, so a
// caller can neither choose nor leak a tenant. Nothing here accepts a member list, a member count or any other piece
// of derived membership - a segment is a definition, and membership is derived on every ask.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>Creates a segment. Status is absent: a segment is always born draft, because putting a rule live is a
/// separate act with its own permission.</summary>
public sealed record CreateSegmentRequest(
    string SegmentCode,
    string SegmentName,
    string SegmentType,
    string SubjectType,
    string MatchMode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    List<SegmentCriteriaNodeRequest>? Criteria);

/// <summary>
/// Updates a segment. <c>SegmentCode</c> and <c>SubjectType</c> are absent because they are immutable.
/// <para>Omitting <c>Criteria</c> entirely leaves the existing tree untouched — which is how the metadata of an ACTIVE
/// (frozen) segment can be edited without tripping the freeze guard. Sending the same tree back is also fine: the
/// guard compares what the rule ASKS, not the ids it arrived with.</para>
/// </summary>
public sealed record UpdateSegmentRequest(
    string SegmentName,
    string SegmentType,
    string SegmentStatus,
    string MatchMode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    List<SegmentCriteriaNodeRequest>? Criteria,
    int? ExpectedVersion);

/// <summary>One node of the embedded predicate tree. A flat list plus <c>ParentNodeId</c> — the runtime assigns the
/// real NodeIds, so an id from another segment can never be smuggled in.</summary>
public sealed record SegmentCriteriaNodeRequest(
    Guid? NodeId,
    Guid? ParentNodeId,
    string NodeKind,
    string? GroupOperator,
    string? AttributeCode,
    string? Operator,
    List<string>? Values,
    string? ValueType,
    Dictionary<string, string>? Parameters,
    bool Negate,
    int SortOrder,
    string? Label)
{
    public SegmentCriteriaNodeInput ToInput() => new(
        NodeId, ParentNodeId, NodeKind, GroupOperator, AttributeCode, Operator, Values, ValueType,
        Parameters, Negate, SortOrder, Label);
}

/// <summary>Adds one hand-written membership row. <c>SelectionReason</c> is required: a manual membership without a
/// reason is not authorable.</summary>
public sealed record AddTargetCustomerRequest(
    string SubjectType,
    Guid SubjectId,
    string MembershipMode,
    string SelectionReason,
    List<string> ReasonCodes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? SubjectDisplayName,
    string? Notes);

/// <summary>Updates one hand-written membership row, including the include-to-exclude switch — which is an update
/// precisely so the pair (segment, subject) never grows a second, contradictory row.</summary>
public sealed record UpdateTargetCustomerRequest(
    string MembershipMode,
    string SelectionReason,
    List<string> ReasonCodes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? SubjectDisplayName,
    string? Notes,
    int? ExpectedVersion);

/// <summary>Resolve options. <c>IncludeExcluded</c> asks for the eliminated candidates WITH their reasons; the counts
/// are always returned either way, so a caller can always check that accepted plus eliminated equals the candidates.</summary>
public sealed record ResolveSegmentMembershipRequest(
    DateTimeOffset? EffectiveAt,
    int? Limit,
    int? Offset,
    bool IncludeExcluded);

/// <summary>The single-subject question. The subject master is not read here; the caller supplies the id.</summary>
public sealed record EvaluateSegmentMembershipRequest(
    string SubjectType,
    Guid SubjectId,
    DateTimeOffset? EffectiveAt);
