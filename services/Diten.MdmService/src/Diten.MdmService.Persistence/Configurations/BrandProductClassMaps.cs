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

            _registered = true;
        }
    }

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
