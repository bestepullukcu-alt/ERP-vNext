namespace Diten.Web.Models.CRM;

/// <summary>Details page view model — the resolved segment plus what the actor is allowed to do with it.</summary>
public sealed class SegmentPageViewModel
{
    public SegmentDetailViewModel Segment { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanActivate { get; set; }
    public bool CanResolve { get; set; }
}

/// <summary>Read model bound from the gateway segment detail response.</summary>
public sealed class SegmentDetailViewModel
{
    public Guid SegmentId { get; set; }
    public string SegmentCode { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string SegmentType { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SegmentStatus { get; set; } = string.Empty;
    public int SegmentVersion { get; set; }
    public Guid VersionLineageId { get; set; }
    public bool Superseded { get; set; }
    public Guid? SupersededBySegmentId { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string MatchMode { get; set; } = string.Empty;
    public List<SegmentCriteriaNodeViewModel> Criteria { get; set; } = new();
    public bool IsCriteriaFrozen { get; set; }
    public DateTimeOffset? CriteriaFrozenAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsArchived { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>One node of the embedded criteria tree. A flat list plus ParentNodeId, exactly as the runtime stores it.</summary>
public sealed class SegmentCriteriaNodeViewModel
{
    public Guid NodeId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public string NodeKind { get; set; } = string.Empty;
    public string? GroupOperator { get; set; }
    public string? AttributeCode { get; set; }
    public string? Operator { get; set; }
    public List<string> Values { get; set; } = new();
    public string? ValueType { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public bool Negate { get; set; }
    public int SortOrder { get; set; }
    public string? Label { get; set; }
}

// ----- gateway envelopes / contract -----

public sealed class SegmentGatewayResponse<T>
{
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public int StatusCode { get; set; }
    public bool IsSuccessful { get; set; }
}

public sealed class SegmentContractViewModel
{
    public bool IsReady { get; set; }
    public SegmentContractFeatures Features { get; set; } = new();
    public SegmentContractVocabularies Vocabularies { get; set; } = new();
    public SegmentContractLimitsViewModel Limits { get; set; } = new();
}

public sealed class SegmentContractFeatures
{
    public bool SupportsSegmentDefinition { get; set; }
    public bool SupportsCriteriaTree { get; set; }
    public bool SupportsRealTimeMembershipResolution { get; set; }
    public bool SupportsManualTargetCustomer { get; set; }
    public bool SupportsProductAffinityAttributes { get; set; }
}

public sealed class SegmentContractVocabularies
{
    public List<string> SegmentTypes { get; set; } = new();
    public List<string> SubjectTypes { get; set; } = new();
    public List<string> SegmentStatuses { get; set; } = new();
    public List<string> MatchModes { get; set; } = new();
    public List<string> CriteriaNodeKinds { get; set; } = new();
    public List<string> GroupOperators { get; set; } = new();
    public List<string> Operators { get; set; } = new();
    public List<string> ValueTypes { get; set; } = new();
    public List<string> MembershipModes { get; set; } = new();
    public List<string> MembershipVerdicts { get; set; } = new();
}

public sealed class SegmentContractLimitsViewModel
{
    public int MaxCriteriaDepth { get; set; }
    public int MaxCriteriaNodes { get; set; }
    public int MaxChildrenPerGroup { get; set; }
    public int MaxValuesPerInOperator { get; set; }
    public int MaxCandidateSet { get; set; }
    public int MaxSegmentsPerSubject { get; set; }
    public bool MembershipIsPersisted { get; set; }
}
