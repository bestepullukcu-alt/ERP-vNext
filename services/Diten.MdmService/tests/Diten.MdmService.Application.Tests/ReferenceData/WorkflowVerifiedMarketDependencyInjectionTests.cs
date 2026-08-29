using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class WorkflowVerifiedMarketDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_registers_distinct_workflow_market_identity_and_client()
    {
        var values = new Dictionary<string, string?>
        {
            [$"{AuthWorkflowVerifiedMarketServiceIdentityProviderOptions.SectionName}:AuthBaseUrl"] = "https://auth.internal/",
            [$"{AuthWorkflowVerifiedMarketServiceIdentityProviderOptions.SectionName}:ExpectedIssuer"] = "issuer",
            [$"{AuthWorkflowVerifiedMarketServiceIdentityProviderOptions.SectionName}:ClientId"] = "reference-client",
            [$"{AuthWorkflowVerifiedMarketServiceIdentityProviderOptions.SectionName}:ActiveClientSecret"] = "reference-secret",
            [$"{VerifiedMarketResolverOptions.SectionName}:PlatformBaseAddress"] = "https://platform.internal/",
            [$"{VerifiedMarketResolverOptions.SectionName}:CredentialIdentifier"] = "resolver-id",
            [$"{VerifiedMarketResolverOptions.SectionName}:CredentialSecret"] = "resolver-secret"
        };
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IWorkflowVerifiedMarketServiceIdentityProvider)
            && descriptor.ImplementationType == typeof(AuthWorkflowVerifiedMarketServiceIdentityProvider)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IWorkflowVerifiedMarketReferenceResolver)
            && descriptor.ImplementationType == typeof(PlatformWorkflowVerifiedMarketResolverClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IVerifiedMarketReferenceResolver)
            && descriptor.ImplementationType == typeof(PlatformWorkflowVerifiedMarketResolverClient));
        Assert.DoesNotContain(
            typeof(PlatformWorkflowVerifiedMarketResolverClient).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType.FullName == "Microsoft.AspNetCore.Http.IHttpContextAccessor");

        using var provider = services.BuildServiceProvider();
        var identityOptions = provider
            .GetRequiredService<IOptions<AuthWorkflowVerifiedMarketServiceIdentityProviderOptions>>().Value;
        Assert.Equal("reference-client", identityOptions.ClientId);
        Assert.Equal("reference-secret", identityOptions.ActiveClientSecret);
    }

    [Fact]
    public void Domain_and_persistence_never_reference_service_token_or_static_credential()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
        {
            "services/Diten.MdmService/src/Diten.MdmService.Domain",
            "services/Diten.MdmService/src/Diten.MdmService.Persistence"
        })
        {
            var files = Directory.GetFiles(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                Assert.DoesNotContain(nameof(WorkflowVerifiedMarketServiceIdentity), source, StringComparison.Ordinal);
                Assert.DoesNotContain("TRUSTED_REFERENCE_DATA_CONSUMER", source, StringComparison.Ordinal);
                Assert.DoesNotContain(PlatformWorkflowVerifiedMarketResolverClient.CredentialSecretHeader, source, StringComparison.Ordinal);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
