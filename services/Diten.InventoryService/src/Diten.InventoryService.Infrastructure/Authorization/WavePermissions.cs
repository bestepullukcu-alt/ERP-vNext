namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0180 permission keys — sub-namespace <c>warehouse.waves.*</c>, owned by MOD-0180 (pack §14, D-4; PKS-001 §4 names the sub-namespace as
/// MOD-0180's exception inside MOD-0178's <c>warehouse.*</c>). All Tenant scope. <see cref="Implemented"/> are the keys the controllers enforce
/// so far — exactly what the manifest declares; release (DL-2), recall and exceptions (DL-3) and KPIs (DL-5) come with their slices. There is
/// no <c>delete</c> key: a Draft wave is cancelled, never deleted (pack §10 G-0180-01).
/// </summary>
public static class WavePermissions
{
    /// <summary>The sub-namespace every MOD-0180 key starts with — and no MOD-0178 key does (pack §14, AC-5).</summary>
    public const string Prefix = "warehouse.waves.";

    /// <summary>Board, list, detail, orders, load, timeline, planner list, warehouse setting read.</summary>
    public const string Read = "warehouse.waves.read";

    /// <summary>Create a wave.</summary>
    public const string Create = "warehouse.waves.create";

    /// <summary>Edit a Draft, add / remove orders, suggestions — NOT the warehouse setting (Q-6 = B).</summary>
    public const string Update = "warehouse.waves.update";

    /// <summary>Write the warehouse wave setting (Tier 3): it changes MOD-0178's task issuance for the whole warehouse.</summary>
    public const string SettingsConfigure = "warehouse.waves.settings.configure";

    /// <summary>Cancel a Draft wave (Tier 3, reason required).</summary>
    public const string Cancel = "warehouse.waves.cancel";

    /// <summary>The keys DL-1's controller enforces (pack §14 "Dilim" column) — exactly the manifest's.</summary>
    public static readonly IReadOnlyList<string> Dl1 = [Read, Create, Update, SettingsConfigure, Cancel];

    /// <summary>Every key the controllers enforce so far.</summary>
    public static IReadOnlyList<string> Implemented => Dl1;
}
