using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure;
using Diten.AuthService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceIdentityTokenDependencyInjectionTests
{
    [Fact]
    public void Dedicated_services_are_registered_without_replacing_human_token_service()
    {
        var services = new ServiceCollection();
        services.AddServiceIdentityTokenIssuance(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.IsType<ServiceClientCredentialVerifier>(provider.GetRequiredService<IServiceClientCredentialVerifier>());
        Assert.IsType<ServiceIdentityTokenIssuer>(provider.GetRequiredService<IServiceIdentityTokenIssuer>());
        Assert.Null(provider.GetService<ITokenService>());
    }
}
