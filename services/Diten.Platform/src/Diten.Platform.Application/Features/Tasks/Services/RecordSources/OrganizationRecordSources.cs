using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services.RecordSources;

/*
 * The FIRST TWO record sources, and they exist together on purpose.
 *
 * One source proves nothing: a contract with a single implementation is indistinguishable from a special case
 * wearing an interface. Two sources, resolved down the same path with no branch naming either of them, is the
 * only honest evidence that the third (Product, when it arrives) will not force a rewrite.
 *
 * Both filter IN MEMORY over the tenant's own units and positions, which are hundreds, not thousands. That is a
 * deliberate, bounded choice and not the contract: SearchAsync takes the term and the cap, so a source with a
 * large table pushes both into its own query without any caller changing. When one does, this comment is where
 * to look for why these two did not.
 */

/// <summary>Shared matching so two sources cannot drift into two different ideas of "matches".</summary>
internal static class RecordMatch
{
    public static bool Matches(string? term, params string?[] fields) =>
        string.IsNullOrWhiteSpace(term)
        || fields.Any(field =>
            !string.IsNullOrWhiteSpace(field)
            && field!.Contains(term.Trim(), StringComparison.CurrentCultureIgnoreCase));
}

/// <summary>
/// Organization units — the source behind a "Department" field. Archived and retired units are excluded: a
/// picker that offers a unit nobody works in produces a task addressed to nowhere.
/// </summary>
public sealed class OrganizationUnitRecordSource : ITaskRecordSource
{
    private readonly IOrganizationUnitRepository _units;

    public OrganizationUnitRecordSource(IOrganizationUnitRepository units) => _units = units;

    public string SourceKey => "organization-unit";
    public string ModuleCode => "organization";
    public string LabelResourceKey => TaskFieldOptionSourceLabels.KeyFor("organization-unit");

    public async Task<IReadOnlyList<TaskRecordDto>> SearchAsync(string? term, int take, CancellationToken ct)
    {
        var rows = await LiveAsync(ct);

        return rows
            .Where(unit => RecordMatch.Matches(term, unit.Code, unit.Name))
            .OrderBy(unit => unit.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(TaskRecordSearchLimits.Clamp(take))
            .Select(ToRecord)
            .ToList();
    }

    public async Task<IReadOnlyList<TaskRecordDto>> ResolveAsync(
        IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        // Resolution deliberately ignores the "live" filter that SEARCH applies: a task already points at this
        // unit, and refusing to name a unit that has since been archived would put the raw identity on screen —
        // exactly the thing BL-049 forbids.
        var wanted = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = await _units.GetAllAsync(ct);

        return rows
            .Where(unit => unit.DeletedAt is null && wanted.Contains(unit.Id.ToString()))
            .Select(ToRecord)
            .ToList();
    }

    private async Task<IReadOnlyList<OrganizationUnit>> LiveAsync(CancellationToken ct)
    {
        var rows = await _units.GetAllAsync(ct);
        return rows
            .Where(unit => unit.DeletedAt is null && !unit.IsArchived && unit.Status == OrgUnitStatus.Active)
            .ToList();
    }

    private static TaskRecordDto ToRecord(OrganizationUnit unit) =>
        new(unit.Id.ToString(), unit.Code, unit.Name, null);
}

/// <summary>
/// Positions — the source behind a "Position" field.
///
/// <para>The secondary line carries the ORGANIZATION UNIT, for the reason the assignable-position lookup already
/// documents: two facilities can each own a "QA Specialist", and an unlabelled entry is how work reaches the
/// wrong site. A position whose unit cannot be resolved is skipped rather than shown unlabelled — the same rule,
/// in the same words, as <c>GetTaskAssignmentPositionLookupHandler</c>.</para>
/// </summary>
public sealed class PositionRecordSource : ITaskRecordSource
{
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _units;

    public PositionRecordSource(IPositionRepository positions, IOrganizationUnitRepository units)
    {
        _positions = positions;
        _units = units;
    }

    public string SourceKey => "position";
    public string ModuleCode => "organization";
    public string LabelResourceKey => TaskFieldOptionSourceLabels.KeyFor("position");

    public async Task<IReadOnlyList<TaskRecordDto>> SearchAsync(string? term, int take, CancellationToken ct)
    {
        var (positions, unitById) = await LoadAsync(ct);

        return positions
            .Where(position => !position.IsArchived && position.Status == PositionStatus.Active)
            .Select(position => ToRecord(position, unitById))
            .Where(record => record is not null)
            .Select(record => record!)
            .Where(record => RecordMatch.Matches(term, record.Code, record.Name, record.Secondary))
            .OrderBy(record => record.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(TaskRecordSearchLimits.Clamp(take))
            .ToList();
    }

    public async Task<IReadOnlyList<TaskRecordDto>> ResolveAsync(
        IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        var wanted = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var (positions, unitById) = await LoadAsync(ct);

        return positions
            .Where(position => wanted.Contains(position.Id.ToString()))
            .Select(position => ToRecord(position, unitById))
            .Where(record => record is not null)
            .Select(record => record!)
            .ToList();
    }

    private async Task<(IReadOnlyList<Position> Positions,
        Dictionary<Guid, OrganizationUnit> UnitById)> LoadAsync(CancellationToken ct)
    {
        var positions = await _positions.GetAllAsync(ct);
        var units = await _units.GetAllAsync(ct);
        return (positions, units.ToDictionary(unit => unit.Id));
    }

    private static TaskRecordDto? ToRecord(
        Position position,
        Dictionary<Guid, OrganizationUnit> unitById) =>
        unitById.TryGetValue(position.OrganizationUnitId, out var unit)
            ? new TaskRecordDto(position.Id.ToString(), position.Code, position.Name, unit.Name)
            : null;
}
