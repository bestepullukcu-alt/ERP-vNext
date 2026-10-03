using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

/// <summary>
/// BL-500 FIX2 — the one row rule (<see cref="TenantModuleEntitlementRowActions"/>) measured directly: what each kind of
/// row offers, and the code a handler answers for an action it does not.
/// </summary>
public sealed class TenantModuleEntitlementRowActionsTests
{
    private static TenantModuleEntitlementRowFacts Stored(EntitlementSource source, bool enabled = true, bool expired = false,
        bool core = false, bool baseline = false, bool inCatalog = true) =>
        new(false, false, source, enabled, expired, core, baseline, inCatalog);

    private static TenantModuleEntitlementRowFacts Plan(bool grantsAccess = true, bool core = false, bool baseline = false) =>
        new(true, grantsAccess, null, false, false, core, baseline, true);

    public static TheoryData<string, TenantModuleEntitlementRowFacts, string[]> Offers() => new()
    {
        { "add-on, on", Stored(EntitlementSource.Addon), ["disable", "extendExpiry"] },
        { "add-on, off", Stored(EntitlementSource.Addon, enabled: false), ["enable", "extendExpiry"] },
        { "add-on, on, expired", Stored(EntitlementSource.Addon, expired: true), ["extendExpiry", "disable"] },
        { "add-on, off, expired — never Enable", Stored(EntitlementSource.Addon, enabled: false, expired: true), ["extendExpiry"] },
        { "override, off, expired", Stored(EntitlementSource.ManualOverride, enabled: false, expired: true), ["extendExpiry", "removeOverride"] },
        { "system row", Stored(EntitlementSource.System), [] },
        { "core add-on", Stored(EntitlementSource.Addon, core: true), [] },
        { "core override — removal only", Stored(EntitlementSource.ManualOverride, enabled: false, core: true), ["removeOverride"] },
        { "baseline override — removal only", Stored(EntitlementSource.ManualOverride, baseline: true), ["removeOverride"] },
        { "core plan line", Plan(core: true), [] },
        { "baseline plan line", Plan(baseline: true), [] },
        { "plan line the plan grants", Plan(), ["disable"] },
        { "plan line blocked", Plan(grantsAccess: false), [] },
        { "override of a module that left the catalogue", Stored(EntitlementSource.ManualOverride, enabled: false, inCatalog: false), ["removeOverride"] },
        { "add-on of a module that left the catalogue", Stored(EntitlementSource.Addon, inCatalog: false), ["disable"] },
    };

    [Theory]
    [MemberData(nameof(Offers))]
    public void Every_kind_of_row_offers_exactly_these_actions(string name, TenantModuleEntitlementRowFacts facts, string[] offered)
    {
        Assert.True(offered.SequenceEqual(TenantModuleEntitlementRowActions.For(facts)),
            $"{name}: offers [{string.Join(", ", TenantModuleEntitlementRowActions.For(facts))}]");
    }

    public static TheoryData<string, TenantModuleEntitlementRowFacts, string, string?> Refusals() => new()
    {
        { "core add-on, remove", Stored(EntitlementSource.Addon, core: true), "removeOverride", "ENTITLEMENT_MODULE_CORE" },
        { "core override, enable", Stored(EntitlementSource.ManualOverride, enabled: false, core: true), "enable", "ENTITLEMENT_MODULE_CORE" },
        { "core override, remove — the clean-up path", Stored(EntitlementSource.ManualOverride, core: true), "removeOverride", null },
        { "baseline override, disable", Stored(EntitlementSource.ManualOverride, baseline: true), "disable", "ENTITLEMENT_MODULE_BASELINE" },
        { "baseline override, remove — the clean-up path", Stored(EntitlementSource.ManualOverride, baseline: true), "removeOverride", null },
        { "core plan line, disable", Plan(core: true), "disable", "ENTITLEMENT_MODULE_CORE" },
        { "system row, disable", Stored(EntitlementSource.System), "disable", "ENTITLEMENT_ACTION_NOT_OFFERED" },
        { "add-on, remove", Stored(EntitlementSource.Addon), "removeOverride", "ENTITLEMENT_NOT_MANUAL_OVERRIDE" },
        { "expired and off, enable", Stored(EntitlementSource.Addon, enabled: false, expired: true), "enable", "ENTITLEMENT_ACTION_NOT_OFFERED" },
        { "left the catalogue, enable", Stored(EntitlementSource.Addon, enabled: false, inCatalog: false), "enable", "ENTITLEMENT_MODULE_NOT_FOUND" },
        { "left the catalogue, extend", Stored(EntitlementSource.Addon, inCatalog: false), "extendExpiry", "ENTITLEMENT_MODULE_NOT_FOUND" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void An_action_not_offered_is_refused_with_its_code(string name, TenantModuleEntitlementRowFacts facts, string action, string? code)
    {
        Assert.True(code == TenantModuleEntitlementRowActions.Refuse(action, facts)?.Code, $"{name}");
    }
}
