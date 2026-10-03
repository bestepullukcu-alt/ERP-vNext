using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Diten.SupplyChainService.Tests;
// Q117 (Q103-N1): BSON serializers are process-wide. Persistence/DependencyInjection registers the String Guid and
// DateTimeOffset serializers once, but that throws if a Guid serializer was already looked up — which a typed
// collection in an earlier test does (the Capacity tests never call AddPersistence). When those tests happen to run
// first, every later AddPersistence fails ("There is already a serializer registered for type Guid"): 264/408 in
// Q103 run 3. Running the product's own registration before any test makes a single-process run order-independent.
// No client is created: the service provider is never built, so nothing connects.
internal static class TestAssemblyBsonSetup
{
    [ModuleInitializer]
    internal static void RegisterPersistenceSerializers() =>
        Diten.SupplyChainService.Persistence.DependencyInjection.AddPersistence(new ServiceCollection(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=1",
                ["Mongo:DatabaseName"] = "unused-module-initializer"
            }).Build());
}
