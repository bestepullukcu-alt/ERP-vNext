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
                map.MapIdMember(x => x.Id).SetSerializer(StandardGuidSerializer);
                map.MapMember(x => x.TenantId).SetSerializer(StandardGuidSerializer);
                map.MapMember(x => x.OwnerCompanyId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.BusinessUnitId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.TherapeuticAreaId).SetSerializer(NullableStandardGuidSerializer);
            });

            TryRegister<Product>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapIdMember(x => x.Id).SetSerializer(StandardGuidSerializer);
                map.MapMember(x => x.TenantId).SetSerializer(StandardGuidSerializer);
                map.MapMember(x => x.BrandId).SetSerializer(NullableStandardGuidSerializer);
                map.MapMember(x => x.TherapeuticAreaId).SetSerializer(NullableStandardGuidSerializer);
            });

            TryRegister<BrandProductExternalReference>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
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
        catch (ArgumentException)
        {
            // Lost a registration race — the existing map is equivalent, so this is safe to ignore.
        }
    }
}
