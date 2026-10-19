using System.Net;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class PlanningServiceHealthTests
{
    [Fact]
    public void MissingJwtSecret_FailsStartup()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "",
                ["JwtSettings:Issuer"] = "diten-auth-service",
                ["JwtSettings:Audience"] = "diten-erp"
            }).Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddDemandPlanningFeature(configuration));
        Assert.Contains("JWT secret", error.Message);
    }

    [Fact]
    public async Task HealthWithoutMongoConfiguration_Returns503WithoutTenantData()
    {
        var (app, client) = await StartAsync(null);
        await using (app)
        using (client)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Unhealthy", body);
            Assert.DoesNotContain("tenant", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [ManualDraftMongoFact]
    public async Task HealthWithAvailableMongo_Returns200Anonymously()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Explicit test Mongo URI is required.");
        var (app, client) = await StartAsync(uri);
        await using (app)
        using (client)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Healthy", body);
            Assert.DoesNotContain("tenant", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(string? mongoUri)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = mongoUri,
            ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests"
        });
        builder.Services.AddSingleton<DemandPlanningMongoContext>();
        var app = builder.Build();
        app.MapDemandPlanningHealth();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new InvalidOperationException("Test host did not bind loopback.");
        var client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(address)
        };
        return (app, client);
    }
}
