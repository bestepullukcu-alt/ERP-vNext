using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// Runs at assembly load, before the first test case (same pattern as Diten.Platform's PlatformTestSerializers and
/// Diten.AuthService's AuthTestSerializers): the FIRST Guid serializer this process ever resolves is the one
/// production registers (<c>Diten.MdmService.Persistence.DependencyInjection.RegisterGuidSerializer</c>), and every
/// client a test builds gets the Guid representation production sets on its own client.
///
/// <para>MEASURED 2026-10-01: without this the suite's result depended on test order, in two ways.</para>
///
/// <para>1. Serializer registration in the driver is process-global. A test that serialized a Guid before anything
/// registered Standard made the driver cache its default Guid serializer, and every later
/// <c>TryRegisterSerializer(new GuidSerializer(Standard))</c> threw "There is already a different serializer
/// registered for type Guid" — 48 cases of SelectedAuditIntentDeliveryMongoTests red in one full run and green in
/// the run before it.</para>
///
/// <para>2. The other half (the BL-280 shape): a <c>MongoClient</c> built in a test without an explicit
/// GuidRepresentation writes UuidLegacy while the global serializer reads Standard only. Such a test passed only
/// when it ran BEFORE anything registered Standard — GskuDraftEditLifecycleTests.Repository_ExactExternalOperation…
/// was green in one order and red in the other. The driver-wide default gives every client in this process the
/// production shape, so the result no longer depends on which class ran first.</para>
///
/// <para>Try*, not Register*: an equal serializer already registered is a no-op, a DIFFERENT one still throws.</para>
/// </summary>
internal static class MdmTestSerializers
{
    [ModuleInitializer]
    internal static void Register()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
#pragma warning disable CS0618
        MongoDefaults.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
    }
}
