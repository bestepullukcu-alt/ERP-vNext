using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TepShell;

public sealed record TepShellMetadataRequest(
    string Code,
    string DisplayName,
    TepShellState ShellState,
    TepShellDependencyStatus HcmFoundationState,
    TepShellDependencyStatus PrivacyLegalState,
    TepShellDependencyStatus ConsentBoundaryState,
    TepShellDependencyStatus VisibilityBoundaryState,
    string SourceContractVersion,
    IReadOnlyList<TepShellDependencyStateDto> DependencyStates,
    DateTimeOffset? LastEvaluatedAt,
    int ShellVersion);
