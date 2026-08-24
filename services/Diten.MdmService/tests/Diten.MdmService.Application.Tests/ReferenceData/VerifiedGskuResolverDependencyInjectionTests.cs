using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class VerifiedGskuResolverDependencyInjectionTests
{
    [Fact]
    public void InfrastructureRegistersResolverTypedClientWithoutCredentialDefaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        Assert.Contains(services, item => item.ServiceType == typeof(IVerifiedGskuReferenceResolver));
        var defaults = new VerifiedGskuResolverOptions();
        Assert.Null(defaults.PlatformBaseAddress);
        Assert.Null(defaults.CredentialIdentifier);
        Assert.Null(defaults.CredentialSecret);
        Assert.Equal(TimeSpan.Zero, defaults.Timeout);
    }
}
