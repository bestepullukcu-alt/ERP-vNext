using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;

/// <summary>
/// BL-500 — the stable codes a refused entitlement change carries (<c>reason_code</c> on the envelope, or on the
/// validation problem for the ones a validator raises). The sentence beside the code stays English and is for logs;
/// the Modules tab says the refusal in the reader's language from the code (<c>TenantsIndex.*.resx</c>, en + tr).
/// The bridge is guarded from both sides: <c>frontend/Diten.Web/tests/platform-tenant-modules-screen.test.js</c> reads
/// this class and demands a sentence for every constant, and <c>TenantModuleEntitlementValidationCodeTests</c> demands
/// that every validator rule on these commands answers with one of these constants — never a derived code the screen
/// cannot know.
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
    /// <summary>The list does not offer this action on this row (any more). The screen reloads the list.</summary>
    public const string ActionNotOffered = "ENTITLEMENT_ACTION_NOT_OFFERED";
    /// <summary>A row id and a module code were both sent, and the row belongs to another module.</summary>
    public const string ModuleMismatch = "ENTITLEMENT_MODULE_MISMATCH";

    // Raised by the validators (TenantModuleEntitlementValidators.cs) as curated codes.
    public const string RowVersionRequired = "ENTITLEMENT_ROW_VERSION_REQUIRED";
    public const string ReasonRequired = "ENTITLEMENT_REASON_REQUIRED";
    public const string ReasonTooLong = "ENTITLEMENT_REASON_TOO_LONG";
    public const string ModuleRequired = "ENTITLEMENT_MODULE_REQUIRED";
    public const string SourceInvalid = "ENTITLEMENT_SOURCE_INVALID";
    public const string ExpiryRequired = "ENTITLEMENT_EXPIRY_REQUIRED";
    public const string ExpiryInPast = "ENTITLEMENT_EXPIRY_IN_PAST";

    public static IReadOnlyList<string> All { get; } =
    [
        Stale, NotFound, ModuleNotFound, CoreModule, BaselineModule, AlreadyEntitled, NotManualOverride,
        ActionNotOffered, ModuleMismatch,
        RowVersionRequired, ReasonRequired, ReasonTooLong, ModuleRequired, SourceInvalid, ExpiryRequired, ExpiryInPast
    ];
}

/// <summary>
/// BL-500 — what is known about one row of the Modules tab, read from the same records by the list query and by every
/// command handler: the stored row (or the plan's own line) and the module's catalogue record. Built only by the two
/// factories below, so the list and the handlers cannot read the facts two different ways.
/// </summary>
public sealed record TenantModuleEntitlementRowFacts(
    bool IsProjectionRow,
    bool GrantsAccess,
    EntitlementSource? Source,
    bool StoredIsEnabled,
    bool IsExpired,
    bool IsCoreModule,
    bool IsBaselineModule,
    bool ModuleInCatalog)
{
    /// <summary>A stored entitlement row. <paramref name="module"/> is the catalogue record of the row's own module.</summary>
    public static TenantModuleEntitlementRowFacts Stored(TenantModuleEntitlement row, ModuleCatalogItem? module, DateTimeOffset now) =>
        new(
            IsProjectionRow: false,
            GrantsAccess: false,
            row.Source,
            row.IsEnabled,
            TenantModuleEntitlementAccessEvaluator.IsExpired(row, now),
            module?.IsCoreModule == true,
            module?.IsBaseline == true,
            module is not null);

    /// <summary>
    /// The plan's own line for a module the tenant's plan includes. It grants access only when the plan is what gives
    /// it — an override that blocks or replaces it carries its own row and its own actions.
    /// </summary>
    public static TenantModuleEntitlementRowFacts Projection(TenantModuleEffectiveAccessDto access, ModuleCatalogItem? module) =>
        new(
            IsProjectionRow: true,
            GrantsAccess: access.HasAccess && access.Source == "Plan",
            Source: null,
            StoredIsEnabled: false,
            IsExpired: false,
            module?.IsCoreModule == true,
            module?.IsBaseline == true,
            module is not null);
}

/// <summary>A refused row action: the stable code, the HTTP status and the English sentence for logs.</summary>
public sealed record TenantModuleEntitlementRefusal(string Code, int StatusCode, string Message);

/// <summary>
/// BL-500 — which actions a row of the tenant's Modules tab offers, and the ONE rule every command handler enforces.
/// The list query stamps <see cref="For"/> on every row and the screen draws exactly that, in this order (the first is
/// the row's primary action); every handler asks <see cref="Refuse"/> before it writes, with the same facts. An action
/// the list does not offer is refused by the server — there is no second copy of the rule to drift.
/// </summary>
public static class TenantModuleEntitlementRowActions
{
    public const string Disable = "disable";
    public const string Enable = "enable";
    public const string ExtendExpiry = "extendExpiry";
    public const string RemoveOverride = "removeOverride";

    public static IReadOnlyList<string> All { get; } = [Disable, Enable, ExtendExpiry, RemoveOverride];

    public static IReadOnlyList<string> For(TenantModuleEntitlementRowFacts facts)
    {
        // A core module is always on and a baseline module is entitlement-free: no entitlement action applies to
        // either — with ONE exception (FIX2 A2): a manual override row that exists on such a module can be REMOVED.
        // It cannot be added any more, but one that exists (added before the add path refused core modules) either
        // switches a core module off for the tenant or holds a modules.max slot for good; removing it is the clean-up.
        if (facts.IsCoreModule || facts.IsBaselineModule)
        {
            return !facts.IsProjectionRow && facts.Source == EntitlementSource.ManualOverride ? [RemoveOverride] : [];
        }

        if (facts.IsProjectionRow)
        {
            // A plan's module comes with the plan. The one thing an operator can do to it here is suspend it for this
            // tenant (a manual override row); once suspended, that override row carries the way back.
            return facts.GrantsAccess ? [Disable] : [];
        }

        if (facts.Source is null or EntitlementSource.System)
        {
            return [];
        }

        var actions = new List<string>(4);
        if (facts.IsExpired)
        {
            // FIX2 — what an expired row needs is a new date. Enabling it would change nothing (it is expired, not
            // switched off) and would take a modules.max slot that grants no access, so an expired row never offers
            // Enable. An expired row that is still switched on keeps Disable: that is how its slot is given back.
            actions.Add(ExtendExpiry);
            if (facts.StoredIsEnabled)
            {
                actions.Add(Disable);
            }
        }
        else
        {
            actions.Add(facts.StoredIsEnabled ? Disable : Enable);
            actions.Add(ExtendExpiry);
        }

        if (facts.Source == EntitlementSource.ManualOverride)
        {
            actions.Add(RemoveOverride);
        }

        if (!facts.ModuleInCatalog)
        {
            // A row whose module has left the catalogue can still be taken away — suspended or, if it is an override,
            // removed — but nothing may grant access to a module that no longer exists.
            actions.RemoveAll(action => action is Enable or ExtendExpiry);
        }

        return actions;
    }

    /// <summary>
    /// Null when the list offers <paramref name="action"/> on a row with these facts; otherwise why not, with the code
    /// the screen says it from. The order is from the most specific reason to the general one.
    /// </summary>
    public static TenantModuleEntitlementRefusal? Refuse(string action, TenantModuleEntitlementRowFacts facts)
    {
        if (For(facts).Contains(action))
        {
            return null;
        }

        if (facts.IsCoreModule)
        {
            return new(TenantModuleEntitlementRefusalCodes.CoreModule, 409, "Core system modules carry no entitlement action.");
        }

        if (facts.IsBaselineModule)
        {
            return new(TenantModuleEntitlementRefusalCodes.BaselineModule, 409, "Baseline modules are entitlement-free and carry no entitlement action.");
        }

        if (!facts.ModuleInCatalog && action is Enable or ExtendExpiry)
        {
            return new(TenantModuleEntitlementRefusalCodes.ModuleNotFound, 404, "Module was not found.");
        }

        if (action == RemoveOverride && !facts.IsProjectionRow && facts.Source != EntitlementSource.ManualOverride)
        {
            return new(TenantModuleEntitlementRefusalCodes.NotManualOverride, 409, "Only manual overrides can be removed.");
        }

        return new(TenantModuleEntitlementRefusalCodes.ActionNotOffered, 409, "This action is not offered on this row.");
    }
}
