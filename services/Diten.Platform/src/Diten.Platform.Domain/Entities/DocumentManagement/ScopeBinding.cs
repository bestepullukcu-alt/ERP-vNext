using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

public sealed class ScopeBinding
{
    public OrgBindingScopeType OrgBindingScopeType { get; set; }
    public required Guid OrgBindingScopeId { get; set; }
    public required string ScopeSourceModule { get; set; }
    public ScopeBindingStatus BindingStatus { get; set; } = ScopeBindingStatus.Active;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
}
