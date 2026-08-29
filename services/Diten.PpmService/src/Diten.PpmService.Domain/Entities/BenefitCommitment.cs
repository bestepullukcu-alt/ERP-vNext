using Diten.PpmService.Domain.GateI.BenefitRealization;

namespace Diten.PpmService.Domain.Entities;

public sealed class BenefitCommitment : EntityBase
{
    public string Code { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public Guid InvestmentCaseId { get; private set; }
    public string TargetDescription { get; private set; } = "";
    public DateOnly? TargetDate { get; private set; }
    public BenefitCommitmentLifecycleState LifecycleState { get; private set; } = BenefitCommitmentLifecycleState.Draft;
    public IReadOnlyList<BenefitCommitmentOutcomeReferenceV1> OutcomeReferences => _outcomeReferences;

    private readonly List<BenefitCommitmentOutcomeReferenceV1> _outcomeReferences = [];

    private BenefitCommitment() { }

    public BenefitCommitment(Guid tenantId, Guid actorId, string code, string title, string? description,
        Guid investmentCaseId, string targetDescription, DateOnly? targetDate) : base(tenantId, actorId)
    {
        if (investmentCaseId == Guid.Empty) throw new ArgumentException("InvestmentCaseId is required.", nameof(investmentCaseId));
        InvestmentCaseId = investmentCaseId;
        SetMetadata(code, title, description, targetDescription, targetDate);
    }

    public void Update(Guid actorId, string code, string title, string? description,
        string targetDescription, DateOnly? targetDate)
    {
        SetMetadata(code, title, description, targetDescription, targetDate);
        MarkUpdated(actorId);
    }

    private void SetMetadata(string code, string title, string? description, string targetDescription, DateOnly? targetDate)
    {
        Code = Required(code, 64, nameof(Code));
        Title = Required(title, 200, nameof(Title));
        Description = Optional(description, 2000, nameof(Description));
        TargetDescription = Required(targetDescription, 1000, nameof(TargetDescription));
        TargetDate = targetDate;
    }

    public void AddOutcomeReference(Guid actorId, BenefitCommitmentOutcomeReferenceV1 reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.BenefitCommitmentId != Id)
            throw new InvalidOperationException("The outcome reference is bound to another benefit commitment.");
        if (IsDeleted || LifecycleState is BenefitCommitmentLifecycleState.Closed or BenefitCommitmentLifecycleState.Cancelled)
            throw new InvalidOperationException("Terminal or deleted benefit commitments cannot change outcome references.");
        if (_outcomeReferences.Any(item => item.OutcomeReference.OutcomeId == reference.OutcomeReference.OutcomeId))
            throw new InvalidOperationException("The outcome reference is already attached.");
        _outcomeReferences.Add(reference);
        MarkUpdated(actorId);
    }

    public void RemoveOutcomeReference(Guid actorId, Guid outcomeId)
    {
        if (IsDeleted || LifecycleState is BenefitCommitmentLifecycleState.Closed or BenefitCommitmentLifecycleState.Cancelled)
            throw new InvalidOperationException("Terminal or deleted benefit commitments cannot change outcome references.");
        if (_outcomeReferences.RemoveAll(item => item.OutcomeReference.OutcomeId == outcomeId) != 1)
            throw new InvalidOperationException("The outcome reference is not attached.");
        MarkUpdated(actorId);
    }

    public bool CanTransitionTo(BenefitCommitmentLifecycleState target) => LifecycleState switch
    {
        BenefitCommitmentLifecycleState.Draft => target is BenefitCommitmentLifecycleState.Planned or BenefitCommitmentLifecycleState.Cancelled,
        BenefitCommitmentLifecycleState.Planned => target is BenefitCommitmentLifecycleState.Active or BenefitCommitmentLifecycleState.Cancelled,
        BenefitCommitmentLifecycleState.Active => target is BenefitCommitmentLifecycleState.Closed or BenefitCommitmentLifecycleState.Cancelled,
        _ => false
    };

    public void Transition(Guid actorId, BenefitCommitmentLifecycleState target)
    {
        if (!CanTransitionTo(target)) throw new InvalidOperationException("Invalid BenefitCommitment lifecycle transition.");
        LifecycleState = target;
        MarkUpdated(actorId);
    }
}
