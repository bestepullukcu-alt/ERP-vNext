using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Adapters;

/// <summary>
/// v1 <see cref="ITimeEntryOrgGateway"/> over MOD-0288's in-process seams. "Who sits where" is asked through
/// <see cref="ITaskSeatDirectory"/> — the platform's ONE place for the active-assignment rule — so this module does not
/// grow an eleventh copy of it.
/// </summary>
public sealed class OrgGatewayAdapter : ITimeEntryOrgGateway
{
    private readonly ITaskSeatDirectory _seats;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _units;

    public OrgGatewayAdapter(ITaskSeatDirectory seats, IPositionRepository positions, IOrganizationUnitRepository units)
    {
        _seats = seats;
        _positions = positions;
        _units = units;
    }

    public async Task<TimeEntryPrimarySeat?> PrimarySeatAsync(Guid userId, CancellationToken ct = default)
    {
        // Primary first — the same ordering the working-hours provider and the task create handler read.
        var seats = await _seats.ActiveForUserAsync(userId, ct);
        if (seats.Count == 0)
        {
            return null;
        }

        var position = await _positions.GetByIdAsync(seats[0].PositionId, ct);
        if (position is null)
        {
            return null;
        }

        var unit = await _units.GetByIdAsync(position.OrganizationUnitId, ct);
        return new TimeEntryPrimarySeat(position.Id, unit?.LegalEntityId);
    }

    public async Task<TimeEntryPosition?> PositionAsync(Guid positionId, CancellationToken ct = default)
    {
        var position = await _positions.GetByIdAsync(positionId, ct);
        return position is null
            ? null
            : new TimeEntryPosition(position.Id, position.ReportsToPositionId, position.IsArchived);
    }

    public Task<IReadOnlyList<Guid>> HoldersOfAsync(Guid positionId, CancellationToken ct = default)
        => _seats.HoldersOfAsync(new HashSet<Guid> { positionId }, ct);
}
