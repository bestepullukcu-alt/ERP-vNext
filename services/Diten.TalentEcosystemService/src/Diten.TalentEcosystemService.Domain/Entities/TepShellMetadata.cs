using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepShellMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepShellState ShellState { get; set; } = TepShellState.Draft;
    public TepShellDependencyStatus HcmFoundationState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus PrivacyLegalState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus ConsentBoundaryState { get; set; } = TepShellDependencyStatus.Deferred;
    public TepShellDependencyStatus VisibilityBoundaryState { get; set; } = TepShellDependencyStatus.Deferred;
    public string SourceContractVersion { get; set; } = string.Empty;
    public List<TepShellDependencyState> DependencyStates { get; set; } = [];
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int ShellVersion { get; set; } = 1;
}

public sealed class TepShellDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
