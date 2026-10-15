using Diten.MdmService.Domain.Entities;
using MongoDB.Bson.Serialization;

namespace Diten.MdmService.Persistence.Configurations;

// BL-552 FIX1 item 10 — rollback safety for the Global Product lifecycle request documents. BL-552 added fields to
// them (the retry streak, when a start was last sent); a binary rolled back to before those fields must still read a
// document a newer binary wrote, instead of throwing on an unknown element. The same guard as BrandProductClassMaps
// (SetIgnoreExtraElements); the members are auto-mapped exactly as before, so nothing already stored reads
// differently.
public static class GlobalProductLifecycleClassMaps
{
    private static bool _registered;
    private static readonly object Gate = new();

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            TryRegister<GlobalProductCorrectionOperation>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
            TryRegister<GlobalProductRetirementRequestOperation>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
            TryRegister<GlobalProductIdentityWorkflowOperation>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
            // BL-552 flow 2 FIX1 item 10: measured, not assumed — a map registered elsewhere first (or an auto-map
            // made by an earlier read) would win the race silently; the service does not start on one that does not
            // ignore extra elements, because a rollback would then fail on every document a newer binary wrote.
            Verify<GlobalProductCorrectionOperation>();
            Verify<GlobalProductRetirementRequestOperation>();
            Verify<GlobalProductIdentityWorkflowOperation>();
            _registered = true;
        }
    }

    // Public so a test can show it refuses a map that does not ignore extra elements (flow 2 FIX1 item 10).
    public static void Verify<T>()
    {
        if (!BsonClassMap.LookupClassMap(typeof(T)).IgnoreExtraElements)
            throw new InvalidOperationException(
                $"BL-552: the class map of {typeof(T).Name} does not ignore extra elements; a rolled-back binary could not read documents a newer one wrote.");
    }

    private static void TryRegister<T>(Action<BsonClassMap<T>> configure)
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T))) return;
        try
        {
            BsonClassMap.RegisterClassMap(configure);
        }
        catch (ArgumentException)
        {
            // Lost a registration race — the existing map is equivalent.
        }
    }
}
