using Diten.MdmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.MdmService.Persistence.Configurations;

// MOD-0290-FU02 — explicit class maps for the two new aggregates.
//
// Why this is not optional: a new aggregate that is missing from class-map registration can end up with its
// Guid FKs written as one representation while query filters serialise them as another. The driver does not
// error — the filter simply never matches and reads come back EMPTY while the data plainly exists in the
// collection. That exact failure has already cost this repo a debugging session in CRM
// (AccountTerritoryAssignment: "Assigned To" silently stayed "—"). Pinning every Guid to Standard here makes
// the write path and the query path agree by construction.
//
// Id and TenantId are EntityBase's members: they are NOT re-mapped here (the driver refuses a base-class member in a
// derived map) — the global GuidSerializer(Standard) registered by AddPersistence pins them to Standard already.
//
// SetIgnoreExtraElements(true) is the forward-compatibility guard: a document written by a later slice (extra
// fields) must still deserialise instead of throwing on an unknown element.
public static class BrandProductClassMaps
{
    private static bool _registered;
    private static readonly object Gate = new();

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            TryRegister<Brand>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapMember(x => x.OwnerCompanyId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.BusinessUnitId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.TherapeuticAreaId).SetSerializer(NullableStandardGuidSerializer);
            });

            TryRegister<Product>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapMember(x => x.BrandId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.TherapeuticAreaId).SetSerializer(NullableStandardGuidSerializer);
            });

            TryRegister<BrandProductExternalReference>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });

            // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — the brand trail, pinned the same way as the brand it records.
            TryRegister<BrandHistoryEntry>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapMember(x => x.BrandId).SetSerializer(StandardGuidSerializer);
            });

            TryRegister<BrandAuditSnapshot>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapMember(x => x.OwnerCompanyId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.BusinessUnitId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.TherapeuticAreaId).SetSerializer(NullableStandardGuidSerializer);
            });

            // WP-MDM-BRAND-ACCEPT-01 FIX2 (item 6) — the external-reference reservations, pinned like the records they hold.
            TryRegister<ExternalReferenceReservation>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapMember(x => x.RecordId).SetSerializer(StandardGuidSerializer);
            });

            EnsureForwardCompatible();
            _registered = true;
        }
    }

    /// <summary>
    /// FIX2 (M1) — the maps that are registered must be the maps written above: a document with a field a later slice
    /// added must still read. FIX1 found these maps silently never registered (a swallowed exception); this check makes
    /// that state stop the start instead of surfacing later as a 500 on the first extra-field document.
    /// </summary>
    internal static void EnsureForwardCompatible()
    {
        foreach (var type in ForwardCompatibleTypes)
        {
            var map = BsonClassMap.LookupClassMap(type);
            if (!map.IgnoreExtraElements)
            {
                throw new InvalidOperationException(
                    $"MDM start refused: the BSON class map of {type.Name} does not ignore extra elements, so a document " +
                    "carrying a field written by a later version would fail to read. Register the map before any collection is used.");
            }
        }
    }

    internal static readonly IReadOnlyList<Type> ForwardCompatibleTypes =
    [
        typeof(Brand), typeof(Product), typeof(BrandProductExternalReference), typeof(BrandHistoryEntry),
        typeof(BrandAuditSnapshot), typeof(ExternalReferenceReservation)
    ];

    private static GuidSerializer StandardGuidSerializer => new(GuidRepresentation.Standard);

    private static NullableSerializer<Guid> NullableStandardGuidSerializer =>
        new(new GuidSerializer(GuidRepresentation.Standard));

    private static void TryRegister<T>(Action<BsonClassMap<T>> configure)
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            // Another host in the same process (e.g. a parallel test fixture) already registered it.
            return;
        }

        try
        {
            BsonClassMap.RegisterClassMap(configure);
        }
        catch (ArgumentException) when (BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            // Lost a registration race — the existing map is equivalent, so this is safe to ignore. ANY other
            // ArgumentException is a broken map and must stop the start: WP-MDM-BRAND-ACCEPT-01 FIX1 found these maps
            // re-mapping Id / TenantId, which EntityBase declares — the driver refused with an ArgumentException that
            // this catch swallowed, so no Brand / Product map was ever registered and SetIgnoreExtraElements never held.
        }
    }
}
