using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Persistence;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.S2S;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class S2SCompositionTests
{
    [Fact]
    public void Application_composition_resolves_contract_validator_without_runtime_activation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<DelegatedActorProofV1ContractValidator>());
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IHostedService) &&
            (x.ImplementationType?.Namespace?.Contains("S2S", StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public async Task Persistence_composition_resolves_default_off_s2s_foundation_without_hosted_activation()
    {
        var services = new ServiceCollection();
        var compose = typeof(Diten.AuthService.Persistence.DependencyInjection).GetMethod(
            "AddS2SFoundation",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        compose.Invoke(null, [services]);
        using var provider = services.BuildServiceProvider();

        AssertRegistration<IS2SMongoContext, S2SMongoContext>(services, ServiceLifetime.Singleton);
        AssertRegistration<IServicePrincipalRepository, ServicePrincipalRepository>(services, ServiceLifetime.Scoped);
        AssertRegistration<IServiceCredentialDescriptorRepository, ServiceCredentialDescriptorRepository>(services, ServiceLifetime.Scoped);
        AssertRegistration<IS2SReplayReceiptStore, S2SReplayReceiptStore>(services, ServiceLifetime.Scoped);
        AssertRegistration<IS2SProofAcceptanceCoordinator, S2SProofAcceptanceCoordinator>(services, ServiceLifetime.Scoped);
        AssertRegistration<IPermissionCatalogManifestRegistrar, PermissionCatalogManifestRegistrar>(services, ServiceLifetime.Scoped);
        AssertRegistration<IExplicitRoleGrantProvisioningCoordinator, ExplicitRoleGrantProvisioningCoordinator>(services, ServiceLifetime.Scoped);

        var authorization = await provider.GetRequiredService<IExplicitRoleGrantProvisioningAuthorizer>()
            .AuthorizeAsync(Guid.NewGuid(), Guid.NewGuid(), ExplicitRoleGrantMutationV1.Grant, "test", CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantAuthorizationDecision.Unavailable, authorization);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            (descriptor.ImplementationType?.Namespace?.Contains("S2S", StringComparison.Ordinal) ?? false));
    }

    private static void AssertRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), registration.ImplementationType);
        Assert.Equal(lifetime, registration.Lifetime);
    }
}
