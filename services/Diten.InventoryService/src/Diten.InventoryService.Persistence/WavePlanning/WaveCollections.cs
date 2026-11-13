namespace Diten.InventoryService.Persistence.WavePlanning;

/// <summary>
/// MOD-0180 collections in the InventoryService database (pack §3, §4.6; prefix <c>wms_wave_</c>). A class of its own — never MOD-0178's
/// <c>WarehouseCollections</c> (the MOD-0181 <c>CycleCountCollections</c> pattern). None is stock: no document carries an OnHand / Available /
/// Reserved total (pack §2.3) — the stock is the MOD-0173 ledger's, the tasks and outbounds are MOD-0178's. DL-1 collections; DL-3 adds the
/// exception queue.
/// </summary>
public static class WaveCollections
{
    /// <summary>The waves (mutable head, Version CAS; never deleted — a Draft is cancelled).</summary>
    public const string Waves = "wms_wave_waves";

    /// <summary>The wave–outbound memberships (a partial unique index keeps an order in one active wave, D-8).</summary>
    public const string Orders = "wms_wave_orders";

    /// <summary>The insert-only timeline of the waves and of the warehouse settings (the local GxP trail, pack §8.13).</summary>
    public const string Events = "wms_wave_events";

    /// <summary>One wave setting per warehouse (managed flag, time zone, thresholds).</summary>
    public const string Settings = "wms_wave_settings";

    public static readonly IReadOnlyList<string> All = [Waves, Orders, Events, Settings];
}
