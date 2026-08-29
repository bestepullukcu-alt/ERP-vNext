using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.AccountAssignments;

/// <summary>
/// MOD-0151 FU05A — the single definition of "current account territory coverage" (pack §22.2a).
///
/// <para>FU05 answered "is this assignment current?" from the assignment row alone, so an
/// <c>AccountTerritoryAssignment</c> whose owning <see cref="TerritoryModel"/> had been deactivated / archived /
/// superseded kept reporting as current coverage. Every consumer of current coverage ("which territory is this
/// account in?", "which MR owns it?", the account grid column, FU09 contact-derived coverage, MOD-0155 visit
/// planning) therefore has to run BOTH gates: the model must be operationally valid at the asked instant AND the
/// assignment must be open at that instant.</para>
///
/// <para>This is a <b>read projection</b> guard only. It never mutates an assignment, never ends one, never deletes
/// history: a deactivated model simply stops projecting current coverage while the underlying rows — and every
/// history query over them — stay exactly as they were.</para>
/// </summary>
public static class TerritoryCoverageLifecyclePolicy
{
    /// <summary>The only <c>territory-model-status</c> value that projects current coverage. The published set also
    /// carries draft / review / approved / inactive / superseded / archived — none of them is operationally valid.</summary>
    public const string CurrentModelStatus = "active";

    /// <summary>The only <c>territory-assignment-status</c> value that projects current coverage.</summary>
    public const string CurrentAssignmentStatus = "active";

    /// <summary>Model gate: stored status is <c>active</c>, the row is not soft-deleted, and the model effective
    /// window covers <paramref name="at"/>. A missing model (deleted, cross-tenant, dangling id) is never current.</summary>
    public static bool IsModelCurrent(TerritoryModel? model, DateTimeOffset at)
        => model is not null
           && !model.IsDeleted
           && string.Equals(model.Status, CurrentModelStatus, StringComparison.OrdinalIgnoreCase)
           && Covers(model.EffectiveFrom, model.EffectiveTo, at);

    /// <summary>Assignment gate: status is <c>active</c>, the row is neither soft-deleted nor ended, and its
    /// effective window covers <paramref name="at"/>.</summary>
    public static bool IsAssignmentCurrent(AccountTerritoryAssignment assignment, DateTimeOffset at)
        => !assignment.IsDeleted
           && assignment.EndedAt is null
           && string.Equals(assignment.AssignmentStatus, CurrentAssignmentStatus, StringComparison.OrdinalIgnoreCase)
           && Covers(assignment.EffectiveFrom, assignment.EffectiveTo, at);

    /// <summary>Both gates. <paramref name="models"/> is the lookup of the owning models, keyed by
    /// <see cref="AccountTerritoryAssignment.TerritoryModelId"/>.</summary>
    public static bool IsCurrent(
        AccountTerritoryAssignment assignment,
        IReadOnlyDictionary<Guid, TerritoryModel> models,
        DateTimeOffset at)
        => IsAssignmentCurrent(assignment, at)
           && IsModelCurrent(models.GetValueOrDefault(assignment.TerritoryModelId), at);

    /// <summary>Filters a candidate set down to current coverage. Order is preserved.</summary>
    public static List<AccountTerritoryAssignment> FilterCurrent(
        IEnumerable<AccountTerritoryAssignment> assignments,
        IReadOnlyDictionary<Guid, TerritoryModel> models,
        DateTimeOffset at)
        => assignments.Where(a => IsCurrent(a, models, at)).ToList();

    /// <summary>The distinct owning model ids of the candidate assignments (what to load for the lookup).</summary>
    public static IReadOnlyCollection<Guid> ModelIdsOf(IEnumerable<AccountTerritoryAssignment> assignments)
        => assignments.Select(a => a.TerritoryModelId).Distinct().ToList();

    private static bool Covers(DateTimeOffset from, DateTimeOffset? to, DateTimeOffset at)
        => from <= at && (to is null || to >= at);
}
