using Diten.PpmService.Domain.GateI.DecisionTrace;
using Diten.PpmService.Domain.GateI.FundingScenario;

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
    public GoverningDecisionReferenceV1? GoverningDecisionReference { get; private set; }
    public IReadOnlyList<SupportingDecisionReferenceV1> SupportingDecisionReferences => _supportingDecisionReferences;
    public SelectedBudgetVersionReferenceV1? SelectedBudgetVersionReference { get; private set; }
    public IReadOnlyList<InvestmentCaseScenarioVersionReferenceV1> ScenarioVersionReferences => _scenarioVersionReferences;
    public IReadOnlyList<InvestmentCaseComparatorOutputReferenceV1> ComparatorOutputReferences => _comparatorOutputReferences;
    public SelectedScenarioReferenceV1? SelectedScenarioReference { get; private set; }

    private readonly List<SupportingDecisionReferenceV1> _supportingDecisionReferences = [];
    private readonly List<InvestmentCaseScenarioVersionReferenceV1> _scenarioVersionReferences = [];
    private readonly List<InvestmentCaseComparatorOutputReferenceV1> _comparatorOutputReferences = [];

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

    public void SetGoverningDecision(Guid actorId, GoverningDecisionReferenceV1 reference)
    {
        ValidateContext(reference?.InvestmentCaseContext.InvestmentCaseId ?? Guid.Empty);
        GoverningDecisionReference = reference;
        MarkUpdated(actorId);
    }

    public void RemoveGoverningDecision(Guid actorId)
    {
        if (GoverningDecisionReference is null)
            throw new InvalidOperationException("The governing decision is not attached.");
        GoverningDecisionReference = null;
        MarkUpdated(actorId);
    }

    public void AddSupportingDecision(Guid actorId, SupportingDecisionReferenceV1 reference)
    {
        ValidateContext(reference?.InvestmentCaseContext.InvestmentCaseId ?? Guid.Empty);
        if (_supportingDecisionReferences.Any(item =>
                item.DecisionRevisionReference.DecisionRevisionId == reference!.DecisionRevisionReference.DecisionRevisionId))
            throw new InvalidOperationException("The supporting decision revision is already attached.");
        _supportingDecisionReferences.Add(reference!);
        MarkUpdated(actorId);
    }

    public void RemoveSupportingDecision(Guid actorId, Guid decisionRevisionId)
    {
        if (!_supportingDecisionReferences.RemoveAll(item =>
                item.DecisionRevisionReference.DecisionRevisionId == decisionRevisionId).Equals(1))
            throw new InvalidOperationException("The supporting decision revision is not attached.");
        MarkUpdated(actorId);
    }

    public void SetSelectedBudget(Guid actorId, SelectedBudgetVersionReferenceV1 reference)
    {
        ValidateContext(reference?.InvestmentCaseContext.InvestmentCaseId ?? Guid.Empty);
        SelectedBudgetVersionReference = reference;
        MarkUpdated(actorId);
    }

    public void RemoveSelectedBudget(Guid actorId)
    {
        if (SelectedBudgetVersionReference is null)
            throw new InvalidOperationException("The selected budget version is not attached.");
        SelectedBudgetVersionReference = null;
        MarkUpdated(actorId);
    }

    public void AddScenarioVersion(Guid actorId, InvestmentCaseScenarioVersionReferenceV1 reference)
    {
        ValidateContext(reference?.InvestmentCaseContext.InvestmentCaseId ?? Guid.Empty);
        if (_scenarioVersionReferences.Any(item => item.ScenarioVersionReference.ScenarioVersionId ==
                                                   reference!.ScenarioVersionReference.ScenarioVersionId))
            throw new InvalidOperationException("The scenario version is already attached.");
        _scenarioVersionReferences.Add(reference!);
        MarkUpdated(actorId);
    }

    public void RemoveScenarioVersion(Guid actorId, Guid scenarioVersionId)
    {
        if (_scenarioVersionReferences.RemoveAll(item =>
                item.ScenarioVersionReference.ScenarioVersionId == scenarioVersionId) != 1)
            throw new InvalidOperationException("The scenario version is not attached.");
        MarkUpdated(actorId);
    }

    public void AddComparatorOutput(Guid actorId, InvestmentCaseComparatorOutputReferenceV1 reference)
    {
        ValidateContext(reference?.InvestmentCaseContext.InvestmentCaseId ?? Guid.Empty);
        if (_comparatorOutputReferences.Any(item => item.ComparatorOutputReference.ComparatorOutputId ==
                                                    reference!.ComparatorOutputReference.ComparatorOutputId))
            throw new InvalidOperationException("The comparator output is already attached.");
        _comparatorOutputReferences.Add(reference!);
        MarkUpdated(actorId);
    }

    public void RemoveComparatorOutput(Guid actorId, Guid comparatorOutputId)
    {
        if (_comparatorOutputReferences.RemoveAll(item =>
                item.ComparatorOutputReference.ComparatorOutputId == comparatorOutputId) != 1)
            throw new InvalidOperationException("The comparator output is not attached.");
        MarkUpdated(actorId);
    }

    public void SetSelectedScenario(Guid actorId, SelectedScenarioReferenceV1 reference)
    {
        ValidateContext(reference?.InvestmentCaseContext.InvestmentCaseId ?? Guid.Empty);
        SelectedScenarioReference = reference;
        MarkUpdated(actorId);
    }

    public void RemoveSelectedScenario(Guid actorId)
    {
        if (SelectedScenarioReference is null)
            throw new InvalidOperationException("The selected scenario is not attached.");
        SelectedScenarioReference = null;
        MarkUpdated(actorId);
    }

    private void ValidateContext(Guid investmentCaseId)
    {
        if (investmentCaseId != Id)
            throw new InvalidOperationException("The Gate I reference is bound to another investment case.");
        if (IsDeleted || LifecycleState is InvestmentCaseLifecycleState.Closed or InvestmentCaseLifecycleState.Withdrawn)
            throw new InvalidOperationException("Terminal or deleted investment cases cannot change Gate I references.");
    }

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
