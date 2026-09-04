using System.Text;

namespace Diten.PpmService.Domain.Entities;

public sealed class Portfolio : EntityBase
{
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public PortfolioLifecycleState LifecycleState { get; private set; } = PortfolioLifecycleState.Draft;
    public string? VisibilityPolicyKey { get; private set; }
    public long InvestmentCaseCollectionFence { get; private set; }
    private Portfolio() { }
    public Portfolio(Guid tenantId, Guid actorId, string code, string name, string? description, string? visibilityPolicyKey) : base(tenantId, actorId)
        => SetMetadata(code, name, description, visibilityPolicyKey);
    public void Update(Guid actorId, string code, string name, string? description, string? visibilityPolicyKey)
    { SetMetadata(code, name, description, visibilityPolicyKey); MarkUpdated(actorId); }
    private void SetMetadata(string code, string name, string? description, string? visibilityPolicyKey)
    { Code = Required(code, 64, nameof(Code)); Name = Required(name, 200, nameof(Name)); Description = Optional(description, 2000, nameof(Description)); VisibilityPolicyKey = Optional(visibilityPolicyKey, 128, nameof(VisibilityPolicyKey)); }
    public bool CanTransitionTo(PortfolioLifecycleState target) => LifecycleState switch
    { PortfolioLifecycleState.Draft => target is PortfolioLifecycleState.Active or PortfolioLifecycleState.Archived, PortfolioLifecycleState.Active => target is PortfolioLifecycleState.Archived, _ => false };
    public void Transition(Guid actorId, PortfolioLifecycleState target)
    { if (!CanTransitionTo(target)) throw new InvalidOperationException($"Invalid Portfolio lifecycle transition: {LifecycleState} -> {target}."); LifecycleState = target; MarkUpdated(actorId); }
    public bool IsReferenceable => !IsDeleted && LifecycleState is PortfolioLifecycleState.Draft or PortfolioLifecycleState.Active;
    public void AdvanceInvestmentCaseCollectionFence() => InvestmentCaseCollectionFence = checked(InvestmentCaseCollectionFence + 1);
}
