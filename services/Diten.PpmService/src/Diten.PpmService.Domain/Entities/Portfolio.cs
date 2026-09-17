namespace Diten.PpmService.Domain.Entities;

public sealed class Portfolio : EntityBase
{
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public string? CapacityAllocationDescription { get; private set; }
    public PortfolioLifecycleState LifecycleState { get; private set; } = PortfolioLifecycleState.Draft;
    public string? VisibilityPolicyKey { get; private set; }
    // Only a newly created, policy-scoped Draft receives this embedded binding.
    // Existing documents remain binding-free and cannot enter this temporary access path.
    public PortfolioTemporaryNonProductionAccessBinding? TemporaryNonProductionAccessBinding { get; private set; }
    public long InvestmentCaseCollectionFence { get; private set; }
    private List<PortfolioOwnerAssignment> _ownerAssignments = [];
    public IReadOnlyList<PortfolioOwnerAssignment> OwnerAssignments => _ownerAssignments.AsReadOnly();
    public PortfolioOwnerAssignment? CurrentOwnerAssignment => _ownerAssignments.LastOrDefault();

    private Portfolio() { }
    public Portfolio(Guid tenantId, Guid actorId, string code, string name, string? description,
        string? visibilityPolicyKey, string? capacityAllocationDescription = null) : base(tenantId, actorId)
        => SetMetadata(code, name, description, visibilityPolicyKey, capacityAllocationDescription);

    public void BindTemporaryNonProductionAccess(PortfolioTemporaryNonProductionAccessBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (IsDeleted || LifecycleState != PortfolioLifecycleState.Draft || Version != 1 || UpdatedAtUtc is not null ||
            TemporaryNonProductionAccessBinding is not null || binding.PortfolioId != Id || !binding.HasValidShape())
            throw new InvalidOperationException("Temporary non-production access binding is immutable and create-only.");
        TemporaryNonProductionAccessBinding = binding;
    }

    public void Update(Guid actorId, string code, string name, string? description,
        string? visibilityPolicyKey, string? capacityAllocationDescription = null)
    {
        if (IsDeleted || LifecycleState != PortfolioLifecycleState.Draft)
            throw new InvalidOperationException("Only Draft Portfolio metadata can be edited.");
        SetMetadata(code, name, description, visibilityPolicyKey, capacityAllocationDescription);
        MarkUpdated(actorId);
    }

    private void SetMetadata(string code, string name, string? description, string? visibilityPolicyKey,
        string? capacityAllocationDescription)
    {
        var nextCode = Required(code, 64, nameof(Code));
        var nextName = Required(name, 200, nameof(Name));
        var nextDescription = Optional(description, 2000, nameof(Description));
        var nextPolicy = Optional(visibilityPolicyKey, 128, nameof(VisibilityPolicyKey));
        var nextCapacity = Optional(capacityAllocationDescription, 2000, nameof(CapacityAllocationDescription));
        Code = nextCode; Name = nextName; Description = nextDescription;
        VisibilityPolicyKey = nextPolicy; CapacityAllocationDescription = nextCapacity;
    }

    public PortfolioOwnerAssignment ChangeOwner(Guid actorId, Guid userId, string displayLabel,
        string reason, PortfolioOwnerOperation operation, Guid? expectedAssignmentId,
        int expectedVersion, Guid requestId, Guid auditIntentId, Guid correlationId)
    {
        if (actorId == Guid.Empty || userId == Guid.Empty || requestId == Guid.Empty ||
            auditIntentId == Guid.Empty || correlationId == Guid.Empty || expectedVersion < 1)
            throw new ArgumentException("Owner assignment identifiers and version are required.");
        var normalizedReason = Required(reason, 2000, nameof(reason));
        var normalizedLabel = Required(displayLabel, 200, nameof(displayLabel));
        if (!Enum.IsDefined(operation)) throw new ArgumentException("Invalid owner operation.");
        if (IsDeleted || LifecycleState != PortfolioLifecycleState.Draft)
            throw new InvalidOperationException("Owner assignment requires a Draft Portfolio.");

        var receipt = _ownerAssignments.SingleOrDefault(x => x.RequestId == requestId);
        if (receipt is not null)
        {
            if (receipt.ActorId != actorId || receipt.UserId != userId || receipt.Reason != normalizedReason ||
                receipt.Operation != operation || receipt.PreviousAssignmentId != expectedAssignmentId ||
                receipt.ExpectedVersion != expectedVersion)
                throw new InvalidOperationException("RequestId was already used with different content.");
            return receipt;
        }
        if (Version != expectedVersion || CurrentOwnerAssignment?.Id != expectedAssignmentId)
            throw new InvalidOperationException("Portfolio owner or version changed.");
        if (operation == PortfolioOwnerOperation.Assign && CurrentOwnerAssignment is not null ||
            operation == PortfolioOwnerOperation.Transfer && CurrentOwnerAssignment is null ||
            CurrentOwnerAssignment?.UserId == userId)
            throw new InvalidOperationException("Owner operation does not match the current assignment.");
        var now = DateTime.UtcNow;
        var occurredAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        var assignment = new PortfolioOwnerAssignment(Guid.NewGuid(), userId, normalizedLabel, actorId,
            normalizedReason, occurredAt, expectedAssignmentId, requestId, expectedVersion,
            checked(Version + 1), operation, auditIntentId, correlationId);
        _ownerAssignments.Add(assignment);
        MarkUpdated(actorId);
        return assignment;
    }

    // Lifecycle mechanics remain for existing aggregate consumers; Portfolio UI/API mutations are closed.
    public bool CanTransitionTo(PortfolioLifecycleState target) => LifecycleState switch
    { PortfolioLifecycleState.Draft => target is PortfolioLifecycleState.Active or PortfolioLifecycleState.Archived,
      PortfolioLifecycleState.Active => target is PortfolioLifecycleState.Archived, _ => false };
    public void Transition(Guid actorId, PortfolioLifecycleState target)
    {
        if (!CanTransitionTo(target)) throw new InvalidOperationException("Invalid Portfolio lifecycle transition.");
        LifecycleState = target; MarkUpdated(actorId);
    }
    public bool IsReferenceable => !IsDeleted && LifecycleState is PortfolioLifecycleState.Draft or PortfolioLifecycleState.Active;
    public void AdvanceInvestmentCaseCollectionFence() => InvestmentCaseCollectionFence = checked(InvestmentCaseCollectionFence + 1);
}
