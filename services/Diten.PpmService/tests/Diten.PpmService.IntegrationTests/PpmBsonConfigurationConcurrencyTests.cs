using Diten.PpmService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Xunit;

namespace Diten.PpmService.IntegrationTests;

public sealed class PpmBsonConfigurationConcurrencyTests
{
    [Fact]
    public async Task Concurrent_configuration_completes_before_callers_return_and_remains_idempotent()
    {
        using var start = new ManualResetEventSlim(false);
        var calls = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                ConfigureThroughPublicSeam();
            }))
            .ToArray();

        start.Set();
        await Task.WhenAll(calls);

        AssertConfigurationIsComplete();

        for (var index = 0; index < 32; index++)
            ConfigureThroughPublicSeam();

        AssertConfigurationIsComplete();
    }

    private static void ConfigureThroughPublicSeam()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPpmPersistence(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:37019/?directConnection=true",
                ["Mongo:DatabaseName"] = "ppm-bson-configuration-only"
            })
            .Build());
    }

    private static void AssertConfigurationIsComplete()
    {
        var guidSerializer = Assert.IsType<GuidSerializer>(BsonSerializer.LookupSerializer<Guid>());
        Assert.Equal(GuidRepresentation.Standard, guidSerializer.GuidRepresentation);

        var dateTimeSerializer = Assert.IsType<DateTimeSerializer>(BsonSerializer.LookupSerializer<DateTime>());
        Assert.Equal(DateTimeKind.Utc, dateTimeSerializer.Kind);

        var document = new ConventionProbe { State = ConventionProbeState.Active }.ToBsonDocument();
        Assert.Equal("Active", document[nameof(ConventionProbe.State)].AsString);
        Assert.False(BsonClassMap.LookupClassMap(typeof(ConventionProbe)).IgnoreExtraElements);
    }

    private sealed class ConventionProbe
    {
        public ConventionProbeState State { get; init; }
    }

    private enum ConventionProbeState
    {
        Active
    }
}
