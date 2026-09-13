using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// Runs at assembly load, before the first test case (same pattern as Diten.Platform's PlatformTestSerializers):
/// the FIRST Guid serializer this process ever registers is the production one, so every later
/// <c>GuidSerializerRegistration.EnsureStandard()</c> — including the real Api host started by AccountKindAcceptance —
/// is the compatible repeat, and the production refusal of an incompatible registration stays exactly as it is.
/// Try*, not Register*: safe if something already registered the same shape.
/// </summary>
internal static class AuthTestSerializers
{
    [ModuleInitializer]
    internal static void Register()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        // The other half (same lesson as PlatformTestSerializers): a MongoClient built in a test without an explicit
        // GuidRepresentation would WRITE UuidLegacy while the global serializer READS Standard only — measured here as
        // ProductAbbreviationPermissionOnboardingMongoTests going red the moment Standard was registered early. The
        // driver-wide default gives every client in this process the production shape.
#pragma warning disable CS0618
        MongoDefaults.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
    }
}
