using System.Text;

namespace Diten.PpmService.Domain.Entities;

public sealed class Program : EntityBase
{
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public Guid? PortfolioId { get; private set; }
    public ProgramLifecycleState LifecycleState { get; private set; } = ProgramLifecycleState.Draft;
    public string? VisibilityPolicyKey { get; private set; }
    private Program() { }
    public Program(Guid tenantId, Guid actorId, string code, string name, string? description, Guid? portfolioId, string? visibilityPolicyKey) : base(tenantId, actorId)
        => SetMetadata(code, name, description, portfolioId, visibilityPolicyKey);
    public void Update(Guid actorId, string code, string name, string? description, Guid? portfolioId, string? visibilityPolicyKey)
    { SetMetadata(code, name, description, portfolioId, visibilityPolicyKey); MarkUpdated(actorId); }
    private void SetMetadata(string code, string name, string? description, Guid? portfolioId, string? visibilityPolicyKey)
    { Code = Required(code, 64, nameof(Code)); Name = Required(name, 200, nameof(Name)); Description = Optional(description, 2000, nameof(Description)); PortfolioId = portfolioId == Guid.Empty ? throw new ArgumentException("PortfolioId cannot be empty.") : portfolioId; VisibilityPolicyKey = Optional(visibilityPolicyKey, 128, nameof(VisibilityPolicyKey)); }
    public bool CanTransitionTo(ProgramLifecycleState target) => LifecycleState switch
    { ProgramLifecycleState.Draft => target is ProgramLifecycleState.Active or ProgramLifecycleState.Cancelled, ProgramLifecycleState.Active => target is ProgramLifecycleState.OnHold or ProgramLifecycleState.Completed or ProgramLifecycleState.Cancelled, ProgramLifecycleState.OnHold => target is ProgramLifecycleState.Active or ProgramLifecycleState.Completed or ProgramLifecycleState.Cancelled, _ => false };
    public void Transition(Guid actorId, ProgramLifecycleState target)
    { if (!CanTransitionTo(target)) throw new InvalidOperationException($"Invalid Program lifecycle transition: {LifecycleState} -> {target}."); LifecycleState = target; MarkUpdated(actorId); }
    public bool IsReferenceable => !IsDeleted && LifecycleState is ProgramLifecycleState.Draft or ProgramLifecycleState.Active or ProgramLifecycleState.OnHold;
}
