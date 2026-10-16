using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-BL576-01 / BL-569 — the Api.Tests twin of Application.Tests' <c>MdmTestSerializers</c>, which this assembly did
/// not have. Runs at assembly load, before any test code: first the Guid serializer production registers, then every
/// document class map (ignore extra elements). Without it, a test that builds a repository or reads a collection before
/// the first host starts makes the driver cache its default Guid serializer (BL-280: empty reads) and auto-map the
/// document unguarded — and every host built later in this process refuses to start on the class-map check.
/// </summary>
internal static class MdmApiTestSerializers
{
    [ModuleInitializer]
    internal static void Register()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
#pragma warning disable CS0618
        MongoDefaults.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        // A refused registration (a map without the flag) is kept and reported by a named test — thrown here it would
        // only break the assembly's load; production (AddPersistence) still refuses to start on it.
        try
        {
            Diten.MdmService.Persistence.Configurations.MdmDocumentClassMaps.Register();
        }
        catch (InvalidOperationException failure)
        {
            RegistrationFailure = failure;
        }
        Ran = true;
    }

    internal static bool Ran { get; private set; }

    internal static Exception? RegistrationFailure { get; private set; }
}
