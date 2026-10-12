namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>
/// MOD-0176 collections in the InventoryService database (pack §3; prefix <c>expiry_</c>, the repo's <c>quarantine_</c> / <c>trace_</c>
/// naming — the pack's camelCase names are the logical names). None is stock: an exception quantity is the quantity the case is
/// about. The audit rows go to the service's <c>inventory_audit</c> (AUD-001).
/// </summary>
public static class ExpiryCollections
{
    public const string Policies = "expiry_policies";

    /// <summary>Insert-only.</summary>
    public const string PolicyHistory = "expiry_policy_history";

    public const string Exceptions = "expiry_exceptions";

    /// <summary>Insert-only.</summary>
    public const string ExceptionEvents = "expiry_exception_events";

    /// <summary>Atomic per Tenant + year counter behind <c>FX-{yyyy}-{nnnn}</c>.</summary>
    public const string Counters = "expiry_counters";

    public static readonly IReadOnlyList<string> All = [Policies, PolicyHistory, Exceptions, ExceptionEvents, Counters];
}
