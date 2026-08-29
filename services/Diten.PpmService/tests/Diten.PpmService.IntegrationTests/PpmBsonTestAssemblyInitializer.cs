using System.Runtime.CompilerServices;
using Diten.PpmService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.PpmService.IntegrationTests;

internal static class PpmBsonTestAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:37019/?directConnection=true",
                ["Mongo:DatabaseName"] = "ppm_bson_test_assembly_initialization"
            })
            .Build();

        new ServiceCollection().AddPpmPersistence(configuration);
    }
}
