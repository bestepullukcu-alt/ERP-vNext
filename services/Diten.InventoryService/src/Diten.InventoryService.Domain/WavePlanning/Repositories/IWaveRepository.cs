using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Rules;

namespace Diten.InventoryService.Domain.WavePlanning.Repositories;

public sealed record WaveQuery(Guid WarehouseId, IReadOnlyCollection<WaveStatus> Statuses, WaveType? WaveType, string? Search, DateTimeOffset? CutoffFrom,
    DateTimeOffset? CutoffTo, int Page, int PageSize);

public sealed record WavePage(IReadOnlyList<Wave> Items, long Total);

public sealed record WaveOrderPage(IReadOnlyList<WaveOrder> Items, long Total);

public sealed record WaveEventPage(IReadOnlyList<WaveEvent> Items, long Total);

/// <summary>What a write transaction did: how many records it changed and the entity the central record names.</summary>
public sealed record WaveWriteOutcome(int Affected, Guid? EntityId);

/// <summary>The unique indexes a MOD-0180 write can hit (pack §4.6).</summary>
public enum WaveUniqueIndex
{
    /// <summary><c>(TenantId, Number)</c>.</summary>
    Number,

    /// <summary><c>(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)</c>.</summary>
    CreateKey,

    /// <summary>The partial unique index on an ACTIVE membership (D-8): the outbound is in another active wave.</summary>
    ActiveOrder,

    /// <summary>An index of <c>wms_wave_events</c> (stream + seq, or a key event).</summary>
    Event,

    /// <summary><c>(TenantId, LegalEntityId, WarehouseId)</c> of <c>wms_wave_settings</c>.</summary>
    Settings
}

public sealed class WaveDuplicateException(WaveUniqueIndex index, Exception? inner = null) : Exception($"Duplicate on {index}.", inner)
{
    public WaveUniqueIndex Index { get; } = index;
}

/// <summary>
/// The membership write found the warehouse no longer wave managed inside its own transaction (pack §8.2 rule 1, §8.19 "Sıralama") — the
/// transaction is abandoned, nothing is written (422 <c>WAVE_SETTINGS_REQUIRED</c>).
/// </summary>
public sealed class WaveGateClosedException() : Exception("The warehouse is not wave managed.");

/// <summary>
/// MOD-0180 waves, memberships, timeline and warehouse settings (pack §3, §4). Every member is bound to the caller's resolved Tenant +
/// LegalEntity — another company's or tenant's wave does not exist here (404). Changes go through <see cref="InTransactionAsync"/>.
/// </summary>
public interface IWaveRepository
{
    Task<Wave?> FindAsync(Guid waveId, CancellationToken cancellationToken = default);

    Task<Wave?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<WavePage> ListAsync(WaveQuery query, CancellationToken cancellationToken = default);

    /// <summary>The totals of the ACTIVE memberships of each wave (a wave without one is absent).</summary>
    Task<IReadOnlyDictionary<Guid, WaveTotals>> TotalsAsync(IReadOnlyCollection<Guid> waveIds, CancellationToken cancellationToken = default);

    Task<WaveOrderPage> OrdersAsync(Guid waveId, IReadOnlyCollection<WaveOrderState> states, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The ACTIVE memberships of one wave.</summary>
    Task<IReadOnlyList<WaveOrder>> ActiveOrdersAsync(Guid waveId, CancellationToken cancellationToken = default);

    /// <summary>Every membership (any state, newest first) the named outbounds have or had in ONE wave.</summary>
    Task<IReadOnlyList<WaveOrder>> MembershipsAsync(Guid waveId, IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default);

    /// <summary>The ACTIVE memberships (in any wave of the company) of the outbounds named.</summary>
    Task<IReadOnlyList<WaveOrder>> ActiveMembershipsAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default);

    /// <summary>The outbounds of one warehouse that are in an active wave — what the candidate list leaves out.</summary>
    Task<IReadOnlyList<Guid>> ActiveOutboundIdsAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>How many active memberships of the warehouse sit in a wave that is <see cref="WaveStatus.Releasing"/>.</summary>
    Task<long> ReleasingOrderCountAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    Task<WaveEventPage> EventsAsync(Guid streamId, WaveEventStream stream, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<WaveEvent?> FindKeyEventAsync(Guid streamId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<WaveWarehouseSettings?> FindSettingsAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> in ONE multi-document transaction; the central-audit event is enlisted only when the work changed a record
    /// (<see cref="WaveWriteOutcome.Affected"/> &gt; 0). A unique-index refusal surfaces as <see cref="WaveDuplicateException"/>.
    /// </summary>
    Task<WaveWriteOutcome> InTransactionAsync(Func<IWaveWriteSession, CancellationToken, Task<WaveWriteOutcome>> work, CancellationToken cancellationToken = default);
}

public interface IWaveWriteSession
{
    Task InsertWaveAsync(Wave wave, CancellationToken cancellationToken = default);

    /// <summary>Replaces the wave if its stored version is still <paramref name="expectedVersion"/>; false = 0 records (a concurrent write won).</summary>
    Task<bool> ReplaceWaveAsync(Wave wave, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertOrdersAsync(IReadOnlyList<WaveOrder> orders, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the wave's <see cref="WaveOrderState.Planned"/> memberships (all of them when <paramref name="outboundIds"/> is null) as
    /// <see cref="WaveOrderState.Removed"/>, <c>Active = false</c> — never a delete — and returns how many it closed.
    /// </summary>
    Task<long> RemovePlannedOrdersAsync(Guid waveId, IReadOnlyCollection<Guid>? outboundIds, Guid closedBy, DateTimeOffset at, string? reason,
        CancellationToken cancellationToken = default);

    Task InsertEventsAsync(IReadOnlyList<WaveEvent> events, CancellationToken cancellationToken = default);

    Task InsertSettingsAsync(WaveWarehouseSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the USER members of the setting (managed flag, time zone, thresholds) with its version and event seq if the stored version is
    /// still <paramref name="expectedVersion"/>; false = 0 records. <see cref="WaveWarehouseSettings.GateSeq"/> is never written here.
    /// </summary>
    Task<bool> UpdateSettingsAsync(WaveWarehouseSettings settings, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pack §8.19 "Sıralama": the conditional write on the warehouse's setting record IN this transaction — it matches only while the warehouse
    /// is wave managed and raises <see cref="WaveWarehouseSettings.GateSeq"/>; no match → <see cref="WaveGateClosedException"/>.
    /// </summary>
    Task PassGateAsync(Guid warehouseId, CancellationToken cancellationToken = default);
}
