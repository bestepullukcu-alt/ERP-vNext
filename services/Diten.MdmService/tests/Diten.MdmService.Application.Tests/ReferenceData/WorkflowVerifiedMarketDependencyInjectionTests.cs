using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class WorkflowVerifiedMarketDependencyInjectionTests
{
    [Fact]
    public void GskuRecoveryUsesDedicatedReferenceIdentityWithoutReplacingInteractiveResolver()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().Build());

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IWorkflowVerifiedMarketServiceIdentityProvider)
            && descriptor.ImplementationType == typeof(AuthWorkflowVerifiedMarketServiceIdentityProvider)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IWorkflowVerifiedGskuReferenceResolver)
            && descriptor.ImplementationType == typeof(PlatformWorkflowVerifiedGskuResolverClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.DoesNotContain(typeof(PlatformWorkflowVerifiedGskuResolverClient).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType.FullName == "Microsoft.AspNetCore.Http.IHttpContextAccessor");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<PlatformWorkflowVerifiedGskuResolverClient>(
            scope.ServiceProvider.GetRequiredService<IWorkflowVerifiedGskuReferenceResolver>());
        Assert.IsType<PlatformVerifiedGskuResolverClient>(
            scope.ServiceProvider.GetRequiredService<IVerifiedGskuReferenceResolver>());
        Assert.Equal("TRUSTED_REFERENCE_DATA_CONSUMER", AuthWorkflowVerifiedMarketServiceIdentityProvider.Audience);
        Assert.Empty(new AuthWorkflowVerifiedMarketServiceIdentityProviderOptions().ActiveClientSecret);
    }

    [Fact]
    public async Task MissingCredentialFailsBeforeHttpRequest()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var identity = provider.GetRequiredService<IWorkflowVerifiedMarketServiceIdentityProvider>();
        var error = await Assert.ThrowsAsync<WorkflowVerifiedMarketServiceIdentityException>(
            () => identity.GetAsync(Guid.NewGuid(), false));
        Assert.Equal("REFERENCE_WORKFLOW_IDENTITY_CONFIGURATION_INVALID", error.ErrorCode);
        Assert.False(error.IsRetryable);
    }
}
