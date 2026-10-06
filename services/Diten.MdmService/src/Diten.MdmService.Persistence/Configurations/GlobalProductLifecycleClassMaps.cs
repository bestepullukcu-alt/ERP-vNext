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
            _registered = true;
        }
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
