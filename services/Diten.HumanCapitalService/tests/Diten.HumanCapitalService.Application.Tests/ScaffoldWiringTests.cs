using Diten.HumanCapitalService.Application;
using Diten.HumanCapitalService.Infrastructure;
using Diten.HumanCapitalService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class ScaffoldWiringTests
{
    [Fact]
    public void Scaffold_services_register_without_business_modules()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:DatabaseName"] = "diten_hcm_test",
                ["MdmService:BaseUrl"] = "http://localhost:5061"
            })
            .Build();

        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }
}
