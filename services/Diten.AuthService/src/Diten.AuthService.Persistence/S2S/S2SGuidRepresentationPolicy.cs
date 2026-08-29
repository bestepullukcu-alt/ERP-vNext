using Diten.AuthService.Domain.S2S;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.AuthService.Persistence.S2S;

internal static class S2SGuidRepresentationPolicy
{
    internal const GuidRepresentation Canonical = GuidRepresentation.Standard;

    private static readonly object Gate = new();
    private static bool _configured;

    internal static void EnsureConfigured()
    {
        if (Volatile.Read(ref _configured)) return;
        lock (Gate)
        {
            if (_configured) return;
            Register<ServicePrincipal>(
                nameof(ServicePrincipal.ServicePrincipalId));
            Register<ServiceCredentialDescriptor>(
                nameof(ServiceCredentialDescriptor.CredentialId),
                nameof(ServiceCredentialDescriptor.ServicePrincipalId));
            Volatile.Write(ref _configured, true);
        }
    }

    private static void Register<T>(params string[] memberNames)
    {
        var serializer = new GuidSerializer(Canonical);
        if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            BsonClassMap.RegisterClassMap<T>(map =>
            {
                map.AutoMap();
                foreach (var memberName in memberNames)
                    map.GetMemberMap(memberName).SetSerializer(serializer);
            });
            return;
        }

        var classMap = BsonClassMap.LookupClassMap(typeof(T));
        foreach (var memberName in memberNames)
        {
            var memberMap = classMap.GetMemberMap(memberName);
            if (memberMap.GetSerializer() is not GuidSerializer existing ||
                existing.GuidRepresentation != Canonical)
                throw new S2SUuidRepresentationIncompatibleException(typeof(T).Name, memberName);
        }
    }
}
