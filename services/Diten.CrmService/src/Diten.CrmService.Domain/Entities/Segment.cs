namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0167 FU02 — <b>Segment</b> aggregate root: the DEFINITION of "who is in this set?", never the list itself.
/// The criteria tree is EMBEDDED in this document (<see cref="Criteria"/>, D2 — a stored, typed predicate tree with
/// <c>ParentNodeId</c>; not a query DSL and not tags). Dynamic membership is NEVER persisted anywhere: it is derived on
/// every ask (D3). Consequently this class deliberately carries no <c>MemberIds</c>, <c>MemberCount</c>,
/// <c>LastResolvedAt</c>, <c>FrequencyCode</c> (MOD-0165), <c>CampaignId</c> (MOD-0165), <c>TerritoryNodeId</c>
/// (MOD-0151), <c>ConsentStatus</c> (MOD-0164) or copied product/brand id (MDM).
/// <para><b>Versioning (D-VER):</b> <see cref="SegmentVersion"/> is the BUSINESS version and is never confused with
/// <see cref="EntityBase.Version"/> (the technical concurrency token). Activating a segment FREEZES its criteria
/// (<see cref="CriteriaFrozenAt"/>); changing the rule means a <c>new-version</c> clone with fresh
/// <see cref="SegmentCriteriaNode.NodeId"/> values and remapped parents. A superseded version stays resolvable so a past
/// selection can still be explained.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload.
/// There is no hard delete — closing a segment is the soft archive lifecycle.</para>
/// </summary>
public sealed class Segment : EntityBase
{
    /// <summary>Stable business key, unique within the tenant among non-archived rows. Never renamed (rename the name).</summary>
    public string SegmentCode { get; set; } = string.Empty;

    public string SegmentName { get; set; } = string.Empty;

    /// <summary><see cref="SegmentTypes"/> — static / dynamic / hybrid.</summary>
    public string SegmentType { get; set; } = SegmentTypes.Dynamic;

    /// <summary><see cref="SegmentSubjectTypes"/> — what the segment groups. IMMUTABLE after create: a segment may not
    /// silently start answering a different question.</summary>
    public string SubjectType { get; set; } = SegmentSubjectTypes.Contact;

    /// <summary><see cref="SegmentStatuses"/> — draft / active / archived.</summary>
    public string SegmentStatus { get; set; } = SegmentStatuses.Draft;

    /// <summary>BUSINESS version (first = 1). NOT <see cref="EntityBase.Version"/>.</summary>
    public int SegmentVersion { get; set; } = 1;

    /// <summary>Root identity binding every version of the same segment. Equals <see cref="EntityBase.Id"/> on v1.</summary>
    public Guid VersionLineageId { get; set; }

    /// <summary>Filled server-side when a newer version is activated. A superseded version stays resolvable.</summary>
    public Guid? SupersededBySegmentId { get; set; }

    /// <summary>Opaque MOD-0048 business-unit code (non-empty string check only; no master read).</summary>
    public string? BusinessUnitId { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Open-ended when null. EffectiveFrom/EffectiveTo are DateTimeOffset (BSON array): never both index keys
    /// and never sorted server-side (parallel-array trap).</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary><see cref="SegmentMatchModes"/> — how the ROOT children combine (all = AND, any = OR).</summary>
    public string MatchMode { get; set; } = SegmentMatchModes.All;

    /// <summary>Embedded predicate tree (flat list + ParentNodeId). Empty for a <c>static</c> segment; at least one
    /// predicate for <c>dynamic</c>/<c>hybrid</c>.</summary>
    public List<SegmentCriteriaNode> Criteria { get; set; } = new();

    public string? Notes { get; set; }

    /// <summary>Stamped at <c>activate</c>. While set, criteria are frozen (409 on any criteria change).</summary>
    public DateTimeOffset? CriteriaFrozenAt { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null
        || string.Equals(SegmentStatus, SegmentStatuses.Archived, StringComparison.Ordinal);

    public bool IsActive() => !IsArchived()
        && string.Equals(SegmentStatus, SegmentStatuses.Active, StringComparison.Ordinal);

    public bool IsCriteriaFrozen() => CriteriaFrozenAt is not null;

    public bool IsSuperseded() => SupersededBySegmentId is not null;

    /// <summary>Effective at the instant. Read-only helper; draws no membership conclusion.</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
}

/// <summary>
/// MOD-0167 FU02 — one node of the EMBEDDED criteria tree (D2). Stored as a flat list on
/// <see cref="Segment.Criteria"/> and shaped into a tree through <see cref="ParentNodeId"/>, so the Mongo class map
/// stays simple, depth validation is a single pass and the UI repeater maps one-to-one. A node is <b>data</b>: it never
/// carries or executes code.
/// </summary>
public sealed class SegmentCriteriaNode
{
    public Guid NodeId { get; set; } = Guid.NewGuid();

    /// <summary><c>null</c> = child of the (implicit) root. Cycles are rejected; the parent must be in the same segment.</summary>
    public Guid? ParentNodeId { get; set; }

    /// <summary><see cref="SegmentCriteriaNodeKinds"/> — group / predicate.</summary>
    public string NodeKind { get; set; } = SegmentCriteriaNodeKinds.Predicate;

    /// <summary><see cref="SegmentGroupOperators"/>. Required for a group, empty for a predicate.</summary>
    public string? GroupOperator { get; set; }

    /// <summary>Required for a predicate; must exist in the closed attribute catalog (free text is rejected).</summary>
    public string? AttributeCode { get; set; }

    /// <summary><see cref="SegmentOperators"/>; must be allowed by the catalog FOR THAT attribute.</summary>
    public string? Operator { get; set; }

    /// <summary>Arity follows the operator: 1 for eq/ne/contains/gt/lt/gte/lte, 2 for between, 1..50 for in/not-in,
    /// 0 for is-null/is-not-null.</summary>
    public List<string> Values { get; set; } = new();

    /// <summary><see cref="SegmentValueTypes"/>; must match the catalog declaration.</summary>
    public string? ValueType { get; set; }

    /// <summary>Attribute-specific context (e.g. consent channel/purpose, account attribute code, affinity maxDepth).
    /// The catalog declares which keys are REQUIRED.</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>Node-level NOT — a shorthand alternative to wrapping the node in a <c>not</c> group.</summary>
    public bool Negate { get; set; }

    /// <summary>Unique among siblings; part of the determinism contract.</summary>
    public int SortOrder { get; set; }

    /// <summary>Display only. NEVER read during evaluation.</summary>
    public string? Label { get; set; }

    public bool IsGroup() => string.Equals(NodeKind, SegmentCriteriaNodeKinds.Group, StringComparison.Ordinal);

    public bool IsPredicate() => string.Equals(NodeKind, SegmentCriteriaNodeKinds.Predicate, StringComparison.Ordinal);
}
