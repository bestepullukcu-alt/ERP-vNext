using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>Small builders so a test reads as the RULE it is about rather than as object construction.</summary>
internal static class SegmentTestBuilders
{
    public static SegmentCriteriaNodeInput Predicate(
        string attributeCode,
        string op,
        string valueType,
        IReadOnlyList<string>? values = null,
        Guid? nodeId = null,
        Guid? parentNodeId = null,
        int sortOrder = 0,
        IReadOnlyDictionary<string, string>? parameters = null,
        bool negate = false)
        => new(
            nodeId ?? Guid.NewGuid(), parentNodeId, SegmentCriteriaNodeKinds.Predicate, null,
            attributeCode, op, values ?? Array.Empty<string>(), valueType, parameters, negate, sortOrder, null);

    public static SegmentCriteriaNodeInput Group(
        string groupOperator, Guid nodeId, Guid? parentNodeId = null, int sortOrder = 0, bool negate = false)
        => new(
            nodeId, parentNodeId, SegmentCriteriaNodeKinds.Group, groupOperator,
            null, null, null, null, null, negate, sortOrder, null);

    /// <summary>The simplest legal contact rule: specialty equals a value.</summary>
    public static List<SegmentCriteriaNodeInput> SpecialtyIs(string value)
        => new()
        {
            Predicate(SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq,
                SegmentValueTypes.String, new[] { value })
        };

    public static Segment Segment(
        Guid tenantId,
        string code = "seg-a",
        string type = SegmentTypes.Dynamic,
        string subjectType = SegmentSubjectTypes.Contact,
        string status = SegmentStatuses.Active,
        List<SegmentCriteriaNode>? criteria = null,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveTo = null,
        string matchMode = SegmentMatchModes.All)
    {
        var id = Guid.NewGuid();
        return new Segment
        {
            Id = id,
            TenantId = tenantId,
            SegmentCode = code,
            SegmentName = code,
            SegmentType = type,
            SubjectType = subjectType,
            SegmentStatus = status,
            SegmentVersion = 1,
            VersionLineageId = id,
            MatchMode = matchMode,
            EffectiveFrom = effectiveFrom ?? SegmentTestDoubles.Past,
            EffectiveTo = effectiveTo,
            Criteria = criteria ?? new List<SegmentCriteriaNode>(),
            CriteriaFrozenAt = status == SegmentStatuses.Active ? SegmentTestDoubles.Past : null
        };
    }

    public static List<SegmentCriteriaNode> Criteria(params SegmentCriteriaNodeInput[] nodes)
        => SegmentMapper.ToCriteria(nodes);

    public static SegmentSubjectSnapshot Contact(
        Guid id, string? specialty = null, string? type = null, string? status = null, string? country = null,
        string? displayName = null)
        => new(id, SegmentSubjectTypes.Contact, displayName, type, null, status, country, null, null, null,
            SegmentTestDoubles.Past, specialty, null, null, null, null);

    public static SegmentSubjectSnapshot Account(
        Guid id, string? type = null, string? category = null, string? status = null, string? country = null,
        string? displayName = null)
        => new(id, SegmentSubjectTypes.Account, displayName, type, category, status, country, null, null, null,
            SegmentTestDoubles.Past, null, null, null, null, null);

    public static TargetCustomer Manual(
        Guid tenantId, Guid segmentId, Guid subjectId, string mode,
        string subjectType = SegmentSubjectTypes.Contact, string? displayName = null)
        => new()
        {
            TenantId = tenantId,
            SegmentId = segmentId,
            SubjectId = subjectId,
            SubjectType = subjectType,
            SubjectDisplayName = displayName,
            MembershipMode = mode,
            SelectionReason = "test",
            ReasonCodes = new List<string>
            {
                mode == SegmentMembershipModes.ManualInclude
                    ? SegmentReasonCodes.ManualInclude
                    : SegmentReasonCodes.ManualExclude
            },
            EffectiveFrom = SegmentTestDoubles.Past
        };

    public static ConceptNode ConceptNode(
        Guid tenantId, Guid subjectId, string? externalRefType, string? externalRefId,
        string status = ConceptStatuses.Active, DateTimeOffset? effectiveTo = null)
        => new()
        {
            TenantId = tenantId,
            SubjectId = subjectId,
            ConceptTypeId = Guid.NewGuid(),
            ConceptNodeCode = "node-" + Guid.NewGuid().ToString("N")[..6],
            ConceptNodeName = "node",
            Status = status,
            EffectiveFrom = SegmentTestDoubles.Past,
            EffectiveTo = effectiveTo,
            ExternalRefType = externalRefType,
            ExternalRefId = externalRefId
        };

    public static ConceptRelationship Edge(
        Guid tenantId, Guid subjectId, Guid from, Guid to, string relationshipType,
        string status = ConceptStatuses.Active, string direction = ConceptDirections.Outbound,
        DateTimeOffset? effectiveTo = null)
        => new()
        {
            TenantId = tenantId,
            SubjectId = subjectId,
            FromConceptNodeId = from,
            ToConceptNodeId = to,
            RelationshipType = relationshipType,
            RelationshipCode = "edge-" + Guid.NewGuid().ToString("N")[..6],
            RelationshipName = "edge",
            Direction = direction,
            Status = status,
            EffectiveFrom = SegmentTestDoubles.Past,
            EffectiveTo = effectiveTo
        };
}
