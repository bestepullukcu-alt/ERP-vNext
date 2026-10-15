using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

public sealed class TrustedLegalEntityScopeProviderDependencyInjectionTests
{
    [Fact]
    public void Infrastructure_registers_distinct_typed_provider_and_request_scoped_facade_without_secret_defaults()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection().Build());

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITrustedLegalEntityScopeProvider));
        var facade = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ProductLegalEntityScopeCandidateFacade));
        Assert.Equal(ServiceLifetime.Scoped, facade.Lifetime);
        var defaults = new TrustedLegalEntityScopeProviderOptions();
        Assert.Null(defaults.PlatformBaseAddress);
        Assert.Null(defaults.CredentialIdentifier);
        Assert.Null(defaults.CredentialSecret);
        Assert.Equal(TimeSpan.FromSeconds(2), defaults.Timeout);
        Assert.NotEqual(VerifiedGskuResolverOptions.SectionName, TrustedLegalEntityScopeProviderOptions.SectionName);
    }

    [Fact]
    public void Options_bind_only_from_the_dedicated_section()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TrustedLegalEntityScopeProvider:PlatformBaseAddress"] = "https://scope.internal/",
            ["TrustedLegalEntityScopeProvider:CredentialIdentifier"] = "id",
            ["TrustedLegalEntityScopeProvider:CredentialSecret"] = "secret",
            ["TrustedLegalEntityScopeProvider:Timeout"] = "00:00:02",
            ["VerifiedGskuResolver:CredentialSecret"] = "wrong-secret"
        }).Build();
        var services = new ServiceCollection(); services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TrustedLegalEntityScopeProviderOptions>>().Value;
        Assert.Equal("scope.internal", options.PlatformBaseAddress!.Host);
        Assert.Equal("secret", options.CredentialSecret);
    }

    [Fact]
    public void Typed_client_disables_redirects_and_redacts_all_sensitive_headers()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root,
            "services", "Diten.MdmService", "src", "Diten.MdmService.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("AllowAutoRedirect = false", source, StringComparison.Ordinal);
        Assert.Contains("RedactLoggedHeaders", source, StringComparison.Ordinal);
        Assert.Contains("\"Authorization\"", source, StringComparison.Ordinal);
        Assert.Contains("CredentialIdHeader", source, StringComparison.Ordinal);
        Assert.Contains("CredentialSecretHeader", source, StringComparison.Ordinal);
        Assert.Contains("AudienceHeader", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
