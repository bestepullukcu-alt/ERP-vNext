using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson.Serialization;

namespace Diten.MdmService.Persistence.Configurations;

// MOD-0290-FU04 S1 (§8.4 B, the BrandProductClassMaps pattern): a document a later slice writes (S2 approval fields,
// S6 conversion details) must still read with this binary — the item's maps ignore extra elements, and the start is
// refused if they do not. Id / TenantId stay EntityBase's (the global Standard GuidSerializer pins them).
public static class ItemMasterClassMaps
{
    private static bool _registered;
    private static readonly object Gate = new();

    internal static readonly IReadOnlyList<Type> ForwardCompatibleTypes = [typeof(Item), typeof(ItemUomConversion)];

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            TryRegister<Item>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
            TryRegister<ItemUomConversion>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
            EnsureForwardCompatible();
            _registered = true;
        }
    }

    internal static void EnsureForwardCompatible()
    {
        foreach (var type in ForwardCompatibleTypes)
        {
            if (!BsonClassMap.LookupClassMap(type).IgnoreExtraElements)
                throw new InvalidOperationException(
                    $"MDM start refused: the BSON class map of {type.Name} does not ignore extra elements, so an item " +
                    "document carrying a field written by a later version would fail to read.");
        }
    }

    private static void TryRegister<T>(Action<BsonClassMap<T>> configure)
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T))) return;
        try
        {
            BsonClassMap.RegisterClassMap(configure);
        }
        catch (ArgumentException) when (BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            // Lost a registration race to an equivalent map; any other ArgumentException stops the start.
        }
    }
}
