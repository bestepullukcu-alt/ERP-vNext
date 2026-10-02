using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;

/// <summary>
/// BL-500 — the stable codes a refused entitlement change carries (<c>reason_code</c> on the envelope). The sentence
/// beside the code stays English and is for logs; the Modules tab says the refusal in the reader's language from the
/// code (<c>TenantsIndex.*.resx</c>, guarded by <c>TenantModulesRefusalBridgeTests</c>).
/// </summary>
public static class TenantModuleEntitlementRefusalCodes
{
    /// <summary>The row changed since the screen loaded it. The screen reloads the list.</summary>
    public const string Stale = "ENTITLEMENT_STALE";
    public const string NotFound = "ENTITLEMENT_NOT_FOUND";
    public const string ModuleNotFound = "ENTITLEMENT_MODULE_NOT_FOUND";
    public const string CoreModule = "ENTITLEMENT_MODULE_CORE";
    public const string BaselineModule = "ENTITLEMENT_MODULE_BASELINE";
    public const string AlreadyEntitled = "ENTITLEMENT_ALREADY_EXISTS";
    public const string NotManualOverride = "ENTITLEMENT_NOT_MANUAL_OVERRIDE";

    public static IReadOnlyList<string> All { get; } =
        [Stale, NotFound, ModuleNotFound, CoreModule, BaselineModule, AlreadyEntitled, NotManualOverride];
}

/// <summary>
/// BL-500 — which actions a row of the tenant's Modules tab offers. ONE place: the list query stamps the answer on
/// every row and the screen draws exactly that, in this order (the first is the row's primary action). The screen
/// keeps no copy of the rule — its copy is how a "remove" button came to sit on a baseline module the server then
/// refused, and an "enable" on an expired row that changed nothing and still said "saved".
///
/// <para>These are the rules the command handlers already enforce, read from the same facts; nothing here changes
/// what is permitted.</para>
/// </summary>
public static class TenantModuleEntitlementRowActions
{
    public const string Disable = "disable";
    public const string Enable = "enable";
    public const string ExtendExpiry = "extendExpiry";
    public const string RemoveOverride = "removeOverride";

    public static IReadOnlyList<string> All { get; } = [Disable, Enable, ExtendExpiry, RemoveOverride];

    /// <param name="isProjectionRow">The row is the plan's own line, not a stored entitlement.</param>
    /// <param name="grantsAccess">Projection row only: the plan currently gives access (no override blocks it).</param>
    /// <param name="source">Stored row only.</param>
    /// <param name="storedIsEnabled">Stored row only: the row's own switch, whatever its expiry.</param>
    /// <param name="isExpired">Stored row only: its expiry date has passed.</param>
    public static IReadOnlyList<string> For(
        bool isProjectionRow,
        bool grantsAccess,
        EntitlementSource? source,
        bool storedIsEnabled,
        bool isExpired,
        bool isCoreModule,
        bool isBaselineModule)
    {
        // A core module is always on and a baseline module is entitlement-free: no entitlement action applies to
        // either, so none is offered (DisableTenantModuleEntitlementCommandHandler and
        // RemoveTenantManualModuleOverrideCommandHandler refuse them).
        if (isCoreModule || isBaselineModule)
        {
            return [];
        }

        if (isProjectionRow)
        {
            // A plan's module comes with the plan. The one thing an operator can do to it here is suspend it for this
            // tenant (a manual override row); once suspended, that override row carries the way back.
            return grantsAccess ? [Disable] : [];
        }

        if (source is null or EntitlementSource.System)
        {
            return [];
        }

        var actions = new List<string>(4);
        if (isExpired)
        {
            // What an expired row needs is a new date. Enabling it changes nothing: it is expired, not switched off.
            actions.Add(ExtendExpiry);
        }

        actions.Add(storedIsEnabled ? Disable : Enable);

        if (!isExpired)
        {
            actions.Add(ExtendExpiry);
        }

        if (source == EntitlementSource.ManualOverride)
        {
            actions.Add(RemoveOverride);
        }

        return actions;
    }
}
