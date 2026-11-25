using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.ProcurementService.Api.Tests.TestInfrastructure;

/// <summary>
/// One process-wide Guid serializer (standard, subtype 4 — MOD-0140 §8) before any test touches Mongo, whichever test runs
/// first: the real hosts (several per run) and the store-level tests then all agree; the production registration is
/// <c>TryRegisterSerializer</c> and keeps this one.
/// </summary>
internal static class GuidSerializerModuleInitializer
{
    [ModuleInitializer]
    internal static void Register() => BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
}
