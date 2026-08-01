namespace Diten.PpmService.Domain.Entities;

public sealed class InvestmentCase : EntityBase
{
    public string Code { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public Guid PortfolioId { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public InvestmentCaseLifecycleState LifecycleState { get; private set; } = InvestmentCaseLifecycleState.Draft;
    public long BenefitCommitmentCollectionFence { get; private set; }

    private InvestmentCase() { }

    public InvestmentCase(Guid tenantId, Guid actorId, string code, string title, string? description,
        Guid portfolioId, DateOnly? plannedStartDate, DateOnly? plannedEndDate) : base(tenantId, actorId)
    {
        if (portfolioId == Guid.Empty) throw new ArgumentException("PortfolioId is required.", nameof(portfolioId));
        PortfolioId = portfolioId;
        SetMetadata(code, title, description, plannedStartDate, plannedEndDate);
    }

    public void Update(Guid actorId, string code, string title, string? description,
        DateOnly? plannedStartDate, DateOnly? plannedEndDate)
    {
        SetMetadata(code, title, description, plannedStartDate, plannedEndDate);
        MarkUpdated(actorId);
    }

    public void AdvanceBenefitCommitmentCollectionFence() => BenefitCommitmentCollectionFence = checked(BenefitCommitmentCollectionFence + 1);

    private void SetMetadata(string code, string title, string? description,
        DateOnly? plannedStartDate, DateOnly? plannedEndDate)
    {
        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate < plannedStartDate)
            throw new ArgumentException("PlannedEndDate cannot be before PlannedStartDate.", nameof(plannedEndDate));
        Code = Required(code, 64, nameof(Code));
        Title = Required(title, 200, nameof(Title));
        Description = Optional(description, 2000, nameof(Description));
        PlannedStartDate = plannedStartDate;
        PlannedEndDate = plannedEndDate;
    }

    public bool CanTransitionTo(InvestmentCaseLifecycleState target) => LifecycleState switch
    {
        InvestmentCaseLifecycleState.Draft => target is InvestmentCaseLifecycleState.UnderAnalysis or InvestmentCaseLifecycleState.Withdrawn,
        InvestmentCaseLifecycleState.UnderAnalysis => target is InvestmentCaseLifecycleState.Closed or InvestmentCaseLifecycleState.Withdrawn,
        _ => false
    };

    public void Transition(Guid actorId, InvestmentCaseLifecycleState target)
    {
        if (!CanTransitionTo(target)) throw new InvalidOperationException("Invalid InvestmentCase lifecycle transition.");
        LifecycleState = target;
        MarkUpdated(actorId);
    }

    public bool IsReferenceable => !IsDeleted && LifecycleState is InvestmentCaseLifecycleState.Draft or InvestmentCaseLifecycleState.UnderAnalysis;
}
