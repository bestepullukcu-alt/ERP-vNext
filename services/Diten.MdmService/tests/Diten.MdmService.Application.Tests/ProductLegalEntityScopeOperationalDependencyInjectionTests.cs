using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeOperationalDependencyInjectionTests
{
    [Fact]
    public void OperationalRegistration_IsDefaultDisabledScopedAndNeverHostedOrController()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();

        services.AddProductLegalEntityScopeOperational(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ProductLegalEntityScopeOperationalRunner)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(ProductLegalEntityScopeOperationalRunner));
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(ProductLegalEntityScopeOperationalRunner)));
        Assert.False(typeof(ControllerBase).IsAssignableFrom(typeof(ProductLegalEntityScopeOperationalRunner)));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProductLegalEntityScopeOperationalOptions>>().Value;
        Assert.False(options.Enabled);
        Assert.Equal(string.Empty, options.Action);
    }

    [Fact]
    public void PersistenceRegistration_UsesDedicatedReadOnlyProjection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:27017",
            ["Mongo:DatabaseName"] = "diten_mdm_product_scope_operational_di_tests"
        }).Build();
        var services = new ServiceCollection();

        services.AddPersistence(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IProductLegalEntityScopeOperationalReadinessRepository)
            && descriptor.ImplementationType == typeof(ProductLegalEntityScopeOperationalReadinessRepository)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Theory]
    [InlineData("Inspect", true)]
    [InlineData("BootstrapPreparation", false)]
    [InlineData("inspect", false)]
    [InlineData("ActivateEnforced", false)]
    public void ConfigurationPreflight_IsExactAndRunsWithoutServiceResolution(
        string action,
        bool validWithoutMutationIdentities)
    {
        var values = new Dictionary<string, string?>
        {
            ["ProductLegalEntityScope:Operational:Enabled"] = "true",
            ["ProductLegalEntityScope:Operational:Action"] = action,
            ["ProductLegalEntityScope:Operational:TenantId"] = Guid.NewGuid().ToString("D")
        };
        if (string.Equals(action, "BootstrapPreparation", StringComparison.Ordinal)
            && !validWithoutMutationIdentities)
        {
            values["ProductLegalEntityScope:Operational:ActorId"] = null;
            values["ProductLegalEntityScope:Operational:CommandId"] = null;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        if (validWithoutMutationIdentities)
        {
            ProductLegalEntityScopeOperationalConfiguration.EnsureValid(configuration);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() =>
                ProductLegalEntityScopeOperationalConfiguration.EnsureValid(configuration));
        }
    }
}
