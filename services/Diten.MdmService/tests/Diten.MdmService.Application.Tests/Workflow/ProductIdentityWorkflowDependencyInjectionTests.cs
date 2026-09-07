using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.Audit;
using Diten.MdmService.Infrastructure.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Workflow;

public sealed class ProductIdentityWorkflowDependencyInjectionTests
{
    [Fact]
    public void Infrastructure_registers_dedicated_workflow_identity_client_and_request_token_accessor()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection().Build());

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IProductIdentityWorkflowServiceIdentityProvider)
            && descriptor.ImplementationType == typeof(AuthProductIdentityWorkflowServiceIdentityProvider)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IProductIdentityWorkflowClient)
            && descriptor.ImplementationType == typeof(PlatformProductIdentityWorkflowClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IProductIdentityDelegatedTokenAccessor)
            && descriptor.ImplementationType == typeof(HttpContextProductIdentityDelegatedTokenAccessor)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Workflow_options_bind_only_from_their_dedicated_sections()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuthProductIdentityWorkflowServiceIdentityProvider:AuthBaseUrl"] = "https://auth.internal/",
            ["AuthProductIdentityWorkflowServiceIdentityProvider:ExpectedIssuer"] = "issuer",
            ["AuthProductIdentityWorkflowServiceIdentityProvider:ClientId"] = "workflow-client",
            ["AuthProductIdentityWorkflowServiceIdentityProvider:ActiveClientSecret"] = "workflow-secret",
            ["ProductIdentityWorkflowClient:PlatformBaseUrl"] = "https://platform.internal/",
            ["AuthTrustedSourceAuditServiceIdentityProvider:ActiveClientSecret"] = "audit-secret"
        }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var identity = provider
            .GetRequiredService<IOptions<AuthProductIdentityWorkflowServiceIdentityProviderOptions>>().Value;
        var client = provider.GetRequiredService<IOptions<ProductIdentityWorkflowClientOptions>>().Value;
        Assert.Equal("workflow-client", identity.ClientId);
        Assert.Equal("workflow-secret", identity.ActiveClientSecret);
        Assert.Equal("https://platform.internal/", client.PlatformBaseUrl);
        Assert.NotEqual(
            AuthProductIdentityWorkflowServiceIdentityProviderOptions.SectionName,
            AuthTrustedSourceAuditServiceIdentityProviderOptions.SectionName);
    }

    [Fact]
    public void Program_registers_default_disabled_worker_and_explicit_recovery_command_without_secret_literals()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "services", "Diten.MdmService", "src", "Diten.MdmService.Api", "Program.cs"));

        Assert.Contains("AddHostedService<ProductIdentityWorkflowRecoveryWorker>()", source, StringComparison.Ordinal);
        Assert.Contains("ProductIdentityWorkflowRecoveryCommandLine.IsRequested(args)", source, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<FirstGskuIdentityWorkflowRecoveryWorker>()", source, StringComparison.Ordinal);
        Assert.Contains("FirstGskuIdentityWorkflowRecoveryCommandLine.IsRequested(args)", source, StringComparison.Ordinal);
        Assert.Contains("AddScoped<FirstGskuIdentityRetirementProcessor>()", source, StringComparison.Ordinal);
        Assert.Contains("ValidateOnStart()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUSTED_WORKFLOW_CONSUMER", source, StringComparison.Ordinal);
        Assert.False(new Diten.MdmService.Api.Configuration.ProductIdentityWorkflowWorkerOptions().Enabled);
        Assert.False(new Diten.MdmService.Api.Configuration.ProductIdentityWorkflowOptions().Enabled);
        Assert.False(new Diten.MdmService.Api.Configuration.FirstGskuIdentityWorkflowWorkerOptions().Enabled);
        Assert.False(new Diten.MdmService.Api.Configuration.FirstGskuIdentityWorkflowOptions().Enabled);
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
