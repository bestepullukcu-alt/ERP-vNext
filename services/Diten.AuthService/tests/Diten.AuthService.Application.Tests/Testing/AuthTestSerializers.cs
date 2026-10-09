using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
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

        // WP-AUTH-TAKEOVER-DECISIONS-01 (K4, measured 2026-10-01) — the same lesson for CONVENTIONS. A class map is built
        // (and frozen) the first time a type is serialized, with the conventions registered AT THAT MOMENT. Production
        // registers "ignore extra elements" before any Mongo use — AddPersistence for every type, and
        // AddEntitlementReconciliationPersistence for the Domain namespace — so its Role/Permission/RolePermission maps
        // always carry it. In this shared test process a test that builds repositories directly on a MongoClient
        // (ProductLegalEntityScopePermissionOnboardingMongoTests: no host, no fixture, no convention) could freeze those
        // three maps WITHOUT it; the reconciliation store's typed snapshot then threw FormatException on an extra
        // element instead of reaching its PLAN_PRECONDITION_DRIFT check — three cases of
        // ProductIdentityEntitlementReconciliationMongoTests, red only in that order (green alone, green as a class).
        // Registering the production pack here, at assembly load, gives every class map in this process the production
        // shape whatever the test order. Production code is unchanged: this is the test process catching up with it.
        ConventionRegistry.Register(
            "AuthTestProcessIgnoreExtraElements",
            new ConventionPack { new IgnoreExtraElementsConvention(true) },
            _ => true);
    }
}
