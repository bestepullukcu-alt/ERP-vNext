using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Builds the <see cref="WorkReportRowSet"/> the tally takes — a TEST convenience, not a production shape.
///
/// <para><b>⚠ WHY THE TESTS GAINED A HELPER AND PRODUCTION DID NOT.</b> Dilim 1c changed
/// <c>WorkReportTally.Build</c> to take one row set instead of four loose arguments, so that the report's
/// numbers and the lists a click opens are computed from the same object. The tempting way to spare ~20 test
/// call sites was to keep the old signature as an overload — but that overload would have had no production
/// caller, and a production method whose only users are tests is exactly the shape CONTROL TOWER caught in
/// Dilim 1a (<see cref="WorkReportScopeMirror"/>): the tested code and the shipped code drift apart, and the
/// suite keeps passing while the enforcement quietly moves. So the adapter lives here, in the test assembly,
/// where it can only ever lie to a test.</para>
///
/// <para><b>Unattended becomes ROWS.</b> Older tests passed a COUNT, which no list can be built from. They get
/// that many synthetic rows — the tally only ever counts this set, and the ones it counts are indistinguishable
/// from real ones for that purpose.</para>
/// </summary>
internal static class WorkReportSets
{
    /// <summary>A row set from the pieces the tally used to take separately.</summary>
    public static WorkReportRowSet Of(
        IEnumerable<WorkReportRow> touched,
        int unattended = 0,
        IReadOnlyDictionary<Guid, int>? returns = null,
        IEnumerable<WorkReportRow>? openAtPeriodEnd = null) => new(
        touched.ToList(),
        openAtPeriodEnd?.ToList() ?? [],
        Enumerable.Range(0, unattended).Select(_ => Unattended()).ToList(),
        returns ?? new Dictionary<Guid, int>());

    /// <summary>
    /// One placeholder for an unclaimed task. It carries no assignee — which is what puts a row in that set at
    /// all — and nothing else the tally reads for this measure.
    /// </summary>
    private static WorkReportRow Unattended() => new(
        Guid.NewGuid(),
        TaskTypeId: null,
        OrganizationUnitId: Guid.Empty,
        AssigneeUserId: null,
        CreatedByUserId: null,
        PoolPositionId: null,
        Priority: TaskPriority.Medium,
        CreatedAt: DateTimeOffset.UnixEpoch,
        CompletedAt: null,
        CancelledAt: null,
        DueAt: null,
        EstimateHours: null,
        SpentHours: 0m,
        ClosureReasonCode: null,
        Lifecycle: TaskLifecycle.Open);
}
