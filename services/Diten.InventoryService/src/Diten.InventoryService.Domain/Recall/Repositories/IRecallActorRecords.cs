using Diten.InventoryService.Domain.Recall.Entities;

namespace Diten.InventoryService.Domain.Recall.Repositories;

/// <summary>The action records of a set of (user, second) pairs, per store.</summary>
public sealed record RecallActorRecordSet(
    IReadOnlyList<RecallCaseEvent> CaseEvents,
    IReadOnlyList<RecallDrillEvent> DrillEvents,
    IReadOnlyList<RecallAuditRecord> AuditRecords,
    IReadOnlyList<RecallSettingsHistoryEntry> SettingsChanges);

/// <summary>
/// WP-MVP1-NAMES1 (RECALL 1.8.0) — READ ONLY: the records that say WHO did a recall action and under which correlation id — the case events
/// (with the <c>ActorName</c> kept at event time), the drill events, the MOD-0177 audit rows and the settings change rows. A command writes the
/// changed field and its record with ONE time, so a person shown with a time is found by (user, that second). Every store is read ONCE for all
/// the pairs of a response (no read per row). The caller's tenant and company only (the repositories' scope).
/// </summary>
public interface IRecallActorRecords
{
    /// <summary>The records of the given users within the given seconds ([second, second + 1 s) each).</summary>
    Task<RecallActorRecordSet> AtAsync(IReadOnlyCollection<(Guid UserId, DateTimeOffset Second)> pairs, CancellationToken cancellationToken = default);

    /// <summary>The MOD-0177 audit rows of the given subjects (an action shown without its time: the prepared case action).</summary>
    Task<IReadOnlyList<RecallAuditRecord>> AuditRecordsAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken = default);
}
