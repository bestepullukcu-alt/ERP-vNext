namespace Diten.InventoryService.Application.Features.WavePlanning.Ports;

/// <summary>A MOD-0178 / LOCATION read could not be done (pack §8.17, §13) — 503 <c>WAVE_INVENTORY_UNAVAILABLE</c>, nothing written.</summary>
public sealed class WaveInventoryUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The warehouse a wave is planned for: an Active LOCATION <c>Warehouse</c> of the caller's company.</summary>
public sealed record WaveWarehouse(Guid Id, string Code);

/// <summary>The shipping address members a candidate shows (no street lines).</summary>
public sealed record WaveShipTo(string Name, string Country, string? City, string? PostalCode);

/// <summary>
/// One MOD-0178 outbound as a wave sees it (pack §8.2 rule 2): its status and pick-issuance state decide eligibility, the three counts feed the
/// limit warnings (lines, allocations, planned bins — pack §4.2). Read only.
/// </summary>
public sealed record WaveOutbound(Guid Id, string Number, Guid WarehouseId, string? OrderRef, WaveShipTo ShipTo, string Status, string? PickIssuanceState,
    int LineCount, int AllocationCount, int PlannedTaskCount, IReadOnlyList<Guid> PlannedBinIds, DateTimeOffset CreatedAt);

public sealed record WaveCandidateQuery(Guid WarehouseId, string? Search, string? ShipTo, bool IncludeAllocating, IReadOnlyCollection<Guid> ExcludedOutboundIds,
    int Page, int PageSize);

public sealed record WaveCandidatePage(IReadOnlyList<WaveOutbound> Items, long Total);

/// <summary>A pick bin on its LOCATION path (pack §8.7 inputs): its code and the zone key (the Zone's code; without a Zone the StorageLocation's).</summary>
public sealed record WaveBin(Guid Id, string Code, string ZoneKey);

/// <summary>
/// MOD-0180's ONLY way to MOD-0178's outbounds and to LOCATION (pack §2.3 rule 2, AC-16): reads, in process, with <c>Find</c> — never a write,
/// never MOD-0178's repositories. Every read is bound to the caller's Tenant + LegalEntity. A read that cannot be done is
/// <see cref="WaveInventoryUnavailableException"/>.
/// </summary>
public interface IWaveInventoryReader
{
    /// <summary>The warehouse if it is an Active <c>Warehouse</c> node of the caller's company; otherwise null.</summary>
    Task<WaveWarehouse?> WarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>The outbounds of the caller's company among <paramref name="outboundIds"/> (an unknown / foreign id is simply absent).</summary>
    Task<IReadOnlyList<WaveOutbound>> OutboundsAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pack §8.2 rule 2: the outbounds of the warehouse that are <c>Allocated</c> with deferred pick issuance and not excluded, oldest first —
    /// plus, when asked, the <c>Allocating</c> ones (shown grey, not addable).
    /// </summary>
    Task<WaveCandidatePage> CandidatesAsync(WaveCandidateQuery query, CancellationToken cancellationToken = default);

    /// <summary>Pack §4.5 "bekleyen sipariş": <c>Allocated</c> outbounds of the warehouse whose pick issuance is deferred.</summary>
    Task<long> DeferredCountAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WaveBin>> BinsAsync(IReadOnlyCollection<Guid> binIds, CancellationToken cancellationToken = default);
}
