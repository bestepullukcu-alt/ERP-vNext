using System.Text;

namespace Diten.PpmService.Domain.Entities;

public sealed class Project : EntityBase
{
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public ProjectParentType ParentType { get; private set; }
    public Guid ParentId { get; private set; }
    public ProjectLifecycleState LifecycleState { get; private set; } = ProjectLifecycleState.Draft;
    public string? VisibilityPolicyKey { get; private set; }
    private Project() { }
    public Project(Guid tenantId, Guid actorId, string code, string name, string? description, ProjectParentType parentType, Guid parentId, string? visibilityPolicyKey) : base(tenantId, actorId)
        => SetMetadata(code, name, description, parentType, parentId, visibilityPolicyKey);
    public void Update(Guid actorId, string code, string name, string? description, ProjectParentType parentType, Guid parentId, string? visibilityPolicyKey)
    { SetMetadata(code, name, description, parentType, parentId, visibilityPolicyKey); MarkUpdated(actorId); }
    private void SetMetadata(string code, string name, string? description, ProjectParentType parentType, Guid parentId, string? visibilityPolicyKey)
    { if (parentId == Guid.Empty) throw new ArgumentException("ParentId is required."); Code = Required(code, 64, nameof(Code)); Name = Required(name, 200, nameof(Name)); Description = Optional(description, 2000, nameof(Description)); ParentType = parentType; ParentId = parentId; VisibilityPolicyKey = Optional(visibilityPolicyKey, 128, nameof(VisibilityPolicyKey)); }
    public bool CanTransitionTo(ProjectLifecycleState target) => LifecycleState switch
    { ProjectLifecycleState.Draft => target is ProjectLifecycleState.Planned or ProjectLifecycleState.Cancelled, ProjectLifecycleState.Planned => target is ProjectLifecycleState.Active or ProjectLifecycleState.OnHold or ProjectLifecycleState.Cancelled, ProjectLifecycleState.Active => target is ProjectLifecycleState.OnHold or ProjectLifecycleState.Completed or ProjectLifecycleState.Cancelled, ProjectLifecycleState.OnHold => target is ProjectLifecycleState.Active or ProjectLifecycleState.Completed or ProjectLifecycleState.Cancelled, _ => false };
    public void Transition(Guid actorId, ProjectLifecycleState target)
    { if (!CanTransitionTo(target)) throw new InvalidOperationException($"Invalid Project lifecycle transition: {LifecycleState} -> {target}."); LifecycleState = target; MarkUpdated(actorId); }
    public bool IsReferenceable => !IsDeleted && LifecycleState is ProjectLifecycleState.Draft or ProjectLifecycleState.Planned or ProjectLifecycleState.Active or ProjectLifecycleState.OnHold;
}
