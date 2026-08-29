using System.Text;

namespace Diten.PpmService.Domain.Entities;

public sealed class Initiative : EntityBase
{
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public Guid? PortfolioId { get; private set; }
    public InitiativeLifecycleState LifecycleState { get; private set; } = InitiativeLifecycleState.Proposed;
    public string? VisibilityPolicyKey { get; private set; }
    private Initiative() { }
    public Initiative(Guid tenantId, Guid actorId, string code, string name, string? description, Guid? portfolioId, string? visibilityPolicyKey) : base(tenantId, actorId)
        => SetMetadata(code, name, description, portfolioId, visibilityPolicyKey);
    public void Update(Guid actorId, string code, string name, string? description, Guid? portfolioId, string? visibilityPolicyKey)
    { SetMetadata(code, name, description, portfolioId, visibilityPolicyKey); MarkUpdated(actorId); }
    private void SetMetadata(string code, string name, string? description, Guid? portfolioId, string? visibilityPolicyKey)
    { Code = Required(code, 64, nameof(Code)); Name = Required(name, 200, nameof(Name)); Description = Optional(description, 2000, nameof(Description)); PortfolioId = portfolioId == Guid.Empty ? throw new ArgumentException("PortfolioId cannot be empty.") : portfolioId; VisibilityPolicyKey = Optional(visibilityPolicyKey, 128, nameof(VisibilityPolicyKey)); }
    public bool CanTransitionTo(InitiativeLifecycleState target) => LifecycleState switch
    { InitiativeLifecycleState.Proposed => target is InitiativeLifecycleState.Active or InitiativeLifecycleState.Cancelled, InitiativeLifecycleState.Active => target is InitiativeLifecycleState.OnHold or InitiativeLifecycleState.Completed or InitiativeLifecycleState.Cancelled, InitiativeLifecycleState.OnHold => target is InitiativeLifecycleState.Active or InitiativeLifecycleState.Completed or InitiativeLifecycleState.Cancelled, _ => false };
    public void Transition(Guid actorId, InitiativeLifecycleState target)
    { if (!CanTransitionTo(target)) throw new InvalidOperationException($"Invalid Initiative lifecycle transition: {LifecycleState} -> {target}."); LifecycleState = target; MarkUpdated(actorId); }
    public bool IsReferenceable => !IsDeleted && LifecycleState is InitiativeLifecycleState.Proposed or InitiativeLifecycleState.Active or InitiativeLifecycleState.OnHold;
}
