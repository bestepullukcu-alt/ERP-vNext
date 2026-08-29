using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AccountAssignments;

public sealed record ApplyAccountTerritoryAssignmentsCommand(
    Guid ModelId,
    Guid? PreviewRunId,
    IReadOnlyList<ApplyAccountTerritoryAssignmentRow> SelectedRows,
    IReadOnlyList<TerritoryBusinessScope> BusinessScopes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ConflictPolicy,
    bool Override,
    string? OverrideReason,
    string? CorrelationId) : IRequest<Response<AccountTerritoryAssignmentApplyResultDto>>;

public sealed record ApplyAccountTerritoryAssignmentRow(Guid AccountId, Guid TerritoryNodeId, Guid? RuleId);

public sealed record EndAccountTerritoryAssignmentCommand(
    Guid ModelId, Guid AssignmentId, DateTimeOffset? EndDate, string? Reason, string? CorrelationId)
    : IRequest<Response<bool>>;

public sealed record GetAccountTerritoryAssignmentListQuery(Guid ModelId)
    : IRequest<Response<AccountTerritoryAssignmentListDto>>;
public sealed record GetAccountTerritoryAssignmentByIdQuery(Guid ModelId, Guid AssignmentId)
    : IRequest<Response<AccountTerritoryAssignmentDto>>;
public sealed record GetAccountTerritoryAssignmentHistoryQuery(Guid AccountId)
    : IRequest<Response<AccountTerritoryAssignmentListDto>>;
public sealed record GetTerritoryCoverageSummaryQuery(Guid AccountId, DateTimeOffset? EffectiveAt = null)
    : IRequest<Response<TerritoryCoverageSummaryDto>>;

public sealed record AccountTerritoryAssignmentApplyResultDto(
    Guid ModelId, Guid? PreviewRunId, int CreatedCount, int EndedCount,
    IReadOnlyList<AccountTerritoryAssignmentDto> Assignments);

public sealed record AccountTerritoryAssignmentListDto(int TotalCount, IReadOnlyList<AccountTerritoryAssignmentDto> Items);

public sealed record AccountTerritoryAssignmentDto(
    Guid Id, Guid AccountId, string AccountCode, string AccountDisplayName,
    Guid TerritoryModelId, Guid TerritoryNodeId, string TerritoryNodeCode, string TerritoryNodeName,
    IReadOnlyList<TerritoryBusinessScope> BusinessScopes, string AssignmentSource, string AssignmentStatus,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, Guid? AppliedFromPreviewRunId,
    Guid? AppliedRuleId, string? AppliedRuleCode, string ConflictPolicy, string? OverrideReason,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, DateTimeOffset? EndedAt);

public sealed record TerritoryCoverageSummaryDto(
    Guid AccountId, DateTimeOffset EffectiveAt, bool HasCurrentCoverage,
    IReadOnlyList<AccountTerritoryAssignmentDto> CurrentAssignments);

public static class AccountTerritoryAssignmentMapper
{
    public static AccountTerritoryAssignmentDto ToDto(AccountTerritoryAssignment a) => new(
        a.Id, a.AccountId, a.AccountCode, a.AccountDisplayName, a.TerritoryModelId, a.TerritoryNodeId,
        a.TerritoryNodeCode, a.TerritoryNodeName, a.BusinessScopes, a.AssignmentSource, a.AssignmentStatus,
        a.EffectiveFrom, a.EffectiveTo, a.AppliedFromPreviewRunId, a.AppliedRuleId, a.AppliedRuleCode,
        a.ConflictPolicy, a.OverrideReason, a.CreatedAt, a.UpdatedAt, a.EndedAt);
}
