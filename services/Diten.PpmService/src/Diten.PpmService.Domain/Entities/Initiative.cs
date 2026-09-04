using System.Text;

namespace Diten.PpmService.Domain.Entities;

public sealed class Initiative : EntityBase
{
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public Guid? PortfolioId { get; private set; }
    public string? InitiativeTypeCode { get; private set; }
    public string? PriorityCode { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public InitiativeLifecycleState LifecycleState { get; private set; } = InitiativeLifecycleState.Proposed;
    public string? VisibilityPolicyKey { get; private set; }
    public Guid? SupersedesInitiativeId { get; private set; }
    private Initiative() { }
    public Initiative(Guid tenantId, Guid actorId, string code, string name, string? description,
        Guid? portfolioId, string? visibilityPolicyKey) : this(tenantId, actorId, code, name, description,
        portfolioId, null, null, null, null)
    {
        if (visibilityPolicyKey is not null)
            throw new ArgumentException("VisibilityPolicyKey is unavailable until authoritative validation exists.", nameof(visibilityPolicyKey));
    }
    public Initiative(Guid tenantId, Guid actorId, string code, string name, string? description,
        Guid? portfolioId, string? initiativeTypeCode, string? priorityCode, DateOnly? plannedStartDate,
        DateOnly? plannedEndDate, Guid? supersedesInitiativeId = null) : base(tenantId, actorId)
    {
        SupersedesInitiativeId = NonEmpty(supersedesInitiativeId, nameof(SupersedesInitiativeId));
        SetMetadata(code, name, description, portfolioId, initiativeTypeCode, priorityCode, plannedStartDate, plannedEndDate);
    }

    public void Update(Guid actorId, string code, string name, string? description, Guid? portfolioId,
        string? initiativeTypeCode, string? priorityCode, DateOnly? plannedStartDate, DateOnly? plannedEndDate)
    {
        EnsureMutable();
        SetMetadata(code, name, description, portfolioId, initiativeTypeCode, priorityCode, plannedStartDate, plannedEndDate);
        MarkUpdated(actorId);
    }

    private void SetMetadata(string code, string name, string? description, Guid? portfolioId,
        string? initiativeTypeCode, string? priorityCode, DateOnly? plannedStartDate, DateOnly? plannedEndDate)
    {
        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate < plannedStartDate)
            throw new ArgumentException("PlannedEndDate cannot precede PlannedStartDate.", nameof(plannedEndDate));
        Code = Required(code, 64, nameof(Code));
        Name = Required(name, 200, nameof(Name));
        Description = Optional(description, 2000, nameof(Description));
        PortfolioId = NonEmpty(portfolioId, nameof(PortfolioId));
        InitiativeTypeCode = Optional(initiativeTypeCode, 128, nameof(InitiativeTypeCode));
        PriorityCode = Optional(priorityCode, 128, nameof(PriorityCode));
        PlannedStartDate = plannedStartDate;
        PlannedEndDate = plannedEndDate;
        VisibilityPolicyKey = null;
    }
    public bool CanTransitionTo(InitiativeLifecycleState target) => LifecycleState switch
    { InitiativeLifecycleState.Proposed => target is InitiativeLifecycleState.Active or InitiativeLifecycleState.Cancelled, InitiativeLifecycleState.Active => target is InitiativeLifecycleState.OnHold or InitiativeLifecycleState.Completed or InitiativeLifecycleState.Cancelled, InitiativeLifecycleState.OnHold => target is InitiativeLifecycleState.Active or InitiativeLifecycleState.Completed or InitiativeLifecycleState.Cancelled, _ => false };
    public void Transition(Guid actorId, InitiativeLifecycleState target)
    {
        EnsureMutable();
        if (!CanTransitionTo(target)) throw new InvalidOperationException($"Invalid Initiative lifecycle transition: {LifecycleState} -> {target}.");
        LifecycleState = target;
        MarkUpdated(actorId);
    }
    public bool IsReferenceable => !IsDeleted && LifecycleState is InitiativeLifecycleState.Proposed or InitiativeLifecycleState.Active or InitiativeLifecycleState.OnHold;
    public bool IsTerminal => LifecycleState is InitiativeLifecycleState.Completed or InitiativeLifecycleState.Cancelled;
    public bool IsActivationReady => InitiativeTypeCode is not null && PriorityCode is not null
        && PlannedStartDate.HasValue && PlannedEndDate.HasValue;

    private void EnsureMutable()
    {
        if (IsTerminal) throw new InvalidOperationException("Terminal Initiative records are immutable.");
    }

    private static Guid? NonEmpty(Guid? value, string name) =>
        value == Guid.Empty ? throw new ArgumentException($"{name} cannot be empty.", name) : value;
}
