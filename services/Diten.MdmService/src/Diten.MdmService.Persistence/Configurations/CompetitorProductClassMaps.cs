using Diten.MdmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.MdmService.Persistence.Configurations;

// WP-VW-W4 (W4-BE-b2) — explicit class maps for the competitor aggregates (the BrandProductClassMaps reason: a new
// aggregate whose Guids are written in one representation and filtered in another reads back EMPTY without an error).
// SetIgnoreExtraElements(true) everywhere: a document written by a later slice still deserialises.
public static class CompetitorProductClassMaps
{
    private static bool _registered;
    private static readonly object Gate = new();

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;

            TryRegister<CompetitorProduct>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapIdMember(x => x.Id).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                map.MapMember(x => x.TenantId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                map.MapMember(x => x.MergedIntoId).SetSerializer(Nullable);
                map.MapMember(x => x.ProposedFromReportId).SetSerializer(Nullable);
                map.MapMember(x => x.ProposedForProductId).SetSerializer(Nullable);
                map.UnmapMember(x => x.IsSelectable);
            });
            TryRegister<CompetitorProposalDecision>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapIdMember(x => x.Id).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                map.MapMember(x => x.TenantId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                map.MapMember(x => x.ProposalId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                map.MapMember(x => x.TargetId).SetSerializer(Nullable);
            });
            TryRegister<CompetitorProductFields>(Loose);
            TryRegister<CompetitorCountryPrice>(Loose);
            TryRegister<CompetitorPriceHistoryEntry>(Loose);
            TryRegister<CompetitorMergeTrace>(map =>
            {
                Loose(map);
                map.MapMember(x => x.SourceId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
            });
            TryRegister<CompetitorChange>(Loose);
            TryRegister<WorkflowApprovalRound>(map =>
            {
                Loose(map);
                map.MapMember(x => x.WorkflowInstanceId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
            });
            TryRegister<WorkflowApprovalDecision>(Loose);
            _registered = true;
        }
    }

    private static NullableSerializer<Guid> Nullable => new(new GuidSerializer(GuidRepresentation.Standard));

    private static void Loose<T>(BsonClassMap<T> map)
    {
        map.AutoMap();
        map.SetIgnoreExtraElements(true);
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
            // lost a registration race: the existing map is equivalent
        }
    }
}
