namespace Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;

public sealed class ProcessModel : EntityBase
{
    public ProcessModel(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processDefinitionId, string modelCode, string name, string? description)
        : base(id, tenantId, createdAtUtc)
    {
        if (processDefinitionId == Guid.Empty) throw new ArgumentException(nameof(processDefinitionId));
        ProcessDefinitionId = processDefinitionId; ModelCode = ProcessModelingText.Code(modelCode);
        Name = ProcessModelingText.Required(name, 200); Description = ProcessModelingText.Optional(description, 4000);
    }
    public Guid ProcessDefinitionId { get; }
    public string ModelCode { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public int LatestRevisionNumber { get; private set; } = 1;
    public Guid? PublishedVersionId { get; private set; }
    public Guid? OpenVersionId { get; private set; }

    public void Update(string name, string? description, int expectedVersion, DateTime nowUtc)
    {
        if (expectedVersion != Version) throw new InvalidOperationException("stale_concurrency");
        Name = ProcessModelingText.Required(name, 200); Description = ProcessModelingText.Optional(description, 4000); Touch(nowUtc);
    }

    public int AllocateRevision(Guid versionId, int expectedVersion, DateTime nowUtc)
    {
        if (expectedVersion != Version) throw new InvalidOperationException("stale_concurrency");
        if (OpenVersionId.HasValue) throw new InvalidOperationException("open_version_exists");
        var allocated = Version == 0 && LatestRevisionNumber == 1 ? 1 : checked(LatestRevisionNumber + 1);
        LatestRevisionNumber = allocated;
        OpenVersionId = versionId;
        Touch(nowUtc);
        return allocated;
    }

    public void CloseOpenVersion(Guid versionId, int expectedVersion, DateTime nowUtc) { EnsurePointer(versionId, expectedVersion); OpenVersionId = null; Touch(nowUtc); }
    public void PublishVersion(Guid versionId, int expectedVersion, DateTime nowUtc) { EnsurePointer(versionId, expectedVersion); PublishedVersionId = versionId; OpenVersionId = null; Touch(nowUtc); }
    public void RetirePublishedVersion(Guid versionId, int expectedVersion, DateTime nowUtc)
    { if (Version != expectedVersion) throw new InvalidOperationException("stale_concurrency"); if (PublishedVersionId != versionId) throw new InvalidOperationException("lifecycle_conflict"); PublishedVersionId = null; Touch(nowUtc); }
    private void EnsurePointer(Guid versionId, int expectedVersion) { if (Version != expectedVersion) throw new InvalidOperationException("stale_concurrency"); if (versionId == Guid.Empty || OpenVersionId != versionId) throw new InvalidOperationException("lifecycle_conflict"); }
}

public sealed class ProcessActivity : EntityBase
{
    public ProcessActivity(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processModelVersionId, Guid logicalActivityId, string activityCode, string name, string? description, int sortOrder) : base(id,tenantId,createdAtUtc)
    { if(processModelVersionId==Guid.Empty||logicalActivityId==Guid.Empty||sortOrder<0) throw new ArgumentException("Invalid activity."); ProcessModelVersionId=processModelVersionId; LogicalActivityId=logicalActivityId; ActivityCode=ProcessModelingText.Code(activityCode); Name=ProcessModelingText.Required(name,200); Description=ProcessModelingText.Optional(description,4000); SortOrder=sortOrder; }
    public Guid ProcessModelVersionId { get; } public Guid LogicalActivityId { get; } public string ActivityCode { get; } public string Name { get; } public string? Description { get; } public int SortOrder { get; }
}
public sealed class ProcessControlPoint : EntityBase
{
    public ProcessControlPoint(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processModelVersionId, Guid logicalControlPointId, string controlCode, string name, string? description, Guid? logicalActivityId, int sortOrder) : base(id,tenantId,createdAtUtc)
    { if(processModelVersionId==Guid.Empty||logicalControlPointId==Guid.Empty||sortOrder<0) throw new ArgumentException("Invalid control point."); ProcessModelVersionId=processModelVersionId; LogicalControlPointId=logicalControlPointId; ControlCode=ProcessModelingText.Code(controlCode); Name=ProcessModelingText.Required(name,200); Description=ProcessModelingText.Optional(description,4000); LogicalActivityId=logicalActivityId; SortOrder=sortOrder; }
    public Guid ProcessModelVersionId { get; } public Guid LogicalControlPointId { get; } public string ControlCode { get; } public string Name { get; } public string? Description { get; } public Guid? LogicalActivityId { get; } public int SortOrder { get; }
}
public sealed class ProcessRelationship : EntityBase
{
    public ProcessRelationship(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processModelVersionId, Guid fromActivityId, Guid toActivityId, string? conditionLabel, int sortOrder) : base(id,tenantId,createdAtUtc)
    { if(processModelVersionId==Guid.Empty||fromActivityId==Guid.Empty||toActivityId==Guid.Empty||fromActivityId==toActivityId||sortOrder<0) throw new ArgumentException("Invalid relationship."); ProcessModelVersionId=processModelVersionId; FromActivityId=fromActivityId; ToActivityId=toActivityId; ConditionLabel=ProcessModelingText.Optional(conditionLabel,500); SortOrder=sortOrder; }
    public Guid ProcessModelVersionId { get; } public Guid FromActivityId { get; } public Guid ToActivityId { get; } public string? ConditionLabel { get; } public int SortOrder { get; }
}

public sealed class ProcessModelVersion : EntityBase
{
    private List<ProcessActivity> _activities = [];
    private List<ProcessControlPoint> _controlPoints = [];
    private List<ProcessRelationship> _relationships = [];

    public ProcessModelVersion(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processModelId, int revisionNumber, string title, string? description)
        : base(id, tenantId, createdAtUtc)
    {
        if (processModelId == Guid.Empty || revisionNumber < 1) throw new ArgumentException("Invalid model revision.");
        ProcessModelId = processModelId; RevisionNumber = revisionNumber;
        Title = ProcessModelingText.Required(title, 200); Description = ProcessModelingText.Optional(description, 4000);
    }

    public Guid ProcessModelId { get; }
    public int RevisionNumber { get; }
    public ProcessModelVersionState LifecycleState { get; private set; } = ProcessModelVersionState.Draft;
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public DateTime? ValidFromUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? RetiredAtUtc { get; private set; }
    public string ContentHash { get; private set; } = string.Empty;
    public IReadOnlyList<ProcessActivity> Activities => _activities.AsReadOnly();
    public IReadOnlyList<ProcessControlPoint> ControlPoints => _controlPoints.AsReadOnly();
    public IReadOnlyList<ProcessRelationship> Relationships => _relationships.AsReadOnly();

    public void ReplaceDraftContent(string title, string? description, IEnumerable<ProcessActivity> activities,
        IEnumerable<ProcessControlPoint> controlPoints, IEnumerable<ProcessRelationship> relationships,
        int expectedVersion, DateTime nowUtc)
    {
        EnsureState(ProcessModelVersionState.Draft, expectedVersion);
        var a = activities.ToList(); var c = controlPoints.ToList(); var r = relationships.ToList();
        ValidateGraph(a, c, r);
        Title = ProcessModelingText.Required(title, 200); Description = ProcessModelingText.Optional(description, 4000);
        ContentHash = CanonicalContentHash.Compute(new(Title, Description, a, c, r)); _activities = a; _controlPoints = c; _relationships = r; Touch(nowUtc);
    }

    public void RequestReview(int expectedVersion, DateTime nowUtc) { EnsureState(ProcessModelVersionState.Draft, expectedVersion); LifecycleState = ProcessModelVersionState.Review; Touch(nowUtc); }
    public void ReturnToDraft(int expectedVersion, DateTime nowUtc) { EnsureState(ProcessModelVersionState.Review, expectedVersion); LifecycleState = ProcessModelVersionState.Draft; Touch(nowUtc); }
    public void PublishDomainTransitionSpec(int expectedVersion, DateTime nowUtc)
    {
        EnsureState(ProcessModelVersionState.Review, expectedVersion);
        LifecycleState = ProcessModelVersionState.Published; ValidFromUtc = nowUtc; PublishedAtUtc = nowUtc; Touch(nowUtc);
    }
    public void Retire(int expectedVersion, DateTime nowUtc) { EnsureState(ProcessModelVersionState.Published, expectedVersion); LifecycleState = ProcessModelVersionState.Retired; RetiredAtUtc = nowUtc; Touch(nowUtc); }

    private void EnsureState(ProcessModelVersionState required, int expectedVersion)
    { if (Version != expectedVersion) throw new InvalidOperationException("stale_concurrency"); if (LifecycleState != required) throw new InvalidOperationException("lifecycle_conflict"); }

    private void ValidateGraph(List<ProcessActivity> a, List<ProcessControlPoint> c, List<ProcessRelationship> r)
    {
        if(a.Any(x=>x.TenantId!=TenantId||x.ProcessModelVersionId!=Id)||c.Any(x=>x.TenantId!=TenantId||x.ProcessModelVersionId!=Id)||r.Any(x=>x.TenantId!=TenantId||x.ProcessModelVersionId!=Id)) throw new ArgumentException("graph_owner_mismatch");
        if (a.Any(x => x.LogicalActivityId == Guid.Empty || x.SortOrder < 0 || ProcessModelingText.Required(x.Name,200).Length == 0 || ProcessModelingText.Optional(x.Description,4000)?.Length > 4000) || a.Select(x => x.LogicalActivityId).Distinct().Count() != a.Count || a.Select(x => ProcessModelingText.Code(x.ActivityCode)).Distinct(StringComparer.Ordinal).Count() != a.Count) throw new ArgumentException("Invalid activities.");
        var ids = a.Select(x => x.LogicalActivityId).ToHashSet();
        if (c.Any(x => x.LogicalControlPointId == Guid.Empty || x.SortOrder < 0 || ProcessModelingText.Required(x.Name,200).Length == 0 || (x.LogicalActivityId.HasValue && !ids.Contains(x.LogicalActivityId.Value))) || c.Select(x => x.LogicalControlPointId).Distinct().Count() != c.Count || c.Select(x => ProcessModelingText.Code(x.ControlCode)).Distinct(StringComparer.Ordinal).Count() != c.Count) throw new ArgumentException("Invalid control points.");
        if (r.Any(x => x.SortOrder < 0 || x.FromActivityId == x.ToActivityId || !ids.Contains(x.FromActivityId) || !ids.Contains(x.ToActivityId))) throw new ArgumentException("Invalid relationships.");
        if (r.Select(x => (x.FromActivityId, x.ToActivityId, ProcessModelingText.Optional(x.ConditionLabel, 500), x.SortOrder)).Distinct().Count() != r.Count) throw new ArgumentException("Duplicate relationship.");
    }
}
