using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.TepShell;

public static class TepShellMapper
{
    public static TepShellMetadataDto ToDto(TepShellMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ShellState,
            entity.HcmFoundationState,
            entity.PrivacyLegalState,
            entity.ConsentBoundaryState,
            entity.VisibilityBoundaryState,
            entity.SourceContractVersion,
            entity.DependencyStates.Select(ToDto).ToList(),
            entity.LastEvaluatedAt,
            entity.ShellVersion);

    public static TepShellMetadataListItemDto ToListItemDto(TepShellMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ShellState,
            entity.HcmFoundationState,
            entity.PrivacyLegalState,
            entity.ConsentBoundaryState,
            entity.VisibilityBoundaryState,
            entity.LastEvaluatedAt,
            entity.ShellVersion);

    public static TepShellDependencyStateDto ToDto(TepShellDependencyState state) =>
        new(state.DependencyKey, state.State, state.Reason);

    public static TepShellDependencyState ToEntity(TepShellDependencyStateDto state) =>
        new()
        {
            DependencyKey = state.DependencyKey.Trim(),
            State = state.State,
            Reason = string.IsNullOrWhiteSpace(state.Reason) ? null : state.Reason.Trim()
        };
}
