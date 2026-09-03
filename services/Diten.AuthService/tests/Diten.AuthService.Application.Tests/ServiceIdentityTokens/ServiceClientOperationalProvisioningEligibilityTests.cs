using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientOperationalProvisioningEligibilityTests
{
    private static readonly Guid CommandId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Default_disabled_fails_closed()
    {
        var eligibility = Build(new ServiceClientOperationalProvisioningOptions());

        Assert.Throws<ServiceClientOperationalContractException>(() => eligibility.EnsureEligible(Request()));
    }

    [Fact]
    public void Non_development_fails_closed()
    {
        var eligibility = Build(ValidOptions(), Environments.Production);

        Assert.Throws<ServiceClientOperationalContractException>(() => eligibility.EnsureEligible(Request()));
    }

    [Fact]
    public void Exact_immutable_facts_are_required()
    {
        var eligibility = Build(ValidOptions());

        eligibility.EnsureEligible(Request());
        Assert.Throws<ServiceClientOperationalContractException>(() => eligibility.EnsureEligible(
            Request() with { CommandId = Guid.NewGuid() }));
        Assert.Throws<ServiceClientOperationalContractException>(() => eligibility.EnsureEligible(
            Request() with { Audience = "TRUSTED_AUDIT_SOURCE_INGEST" }));
    }

    [Fact]
    public void Secret_operation_requires_pipe_and_read_operation_forbids_it()
    {
        var create = Build(ValidOptions());
        Assert.Throws<ServiceClientOperationalContractException>(() => create.EnsureEligible(
            Request() with { InheritedPipeHandle = null }));

        var readOptions = ValidOptions();
        readOptions.AuthorizedOperation = ServiceClientOperationalOperations.ReadIdentity;
        var read = Build(readOptions);
        Assert.Throws<ServiceClientOperationalContractException>(() => read.EnsureEligible(
            Request() with { Operation = ServiceClientOperationalOperations.ReadIdentity }));
    }

    private static DevelopmentServiceClientOperationalProvisioningEligibility Build(
        ServiceClientOperationalProvisioningOptions options,
        string environment = "Development") =>
        new(new TestEnvironment(environment), Options.Create(options));

    private static ServiceClientOperationalProvisioningOptions ValidOptions() => new()
    {
        Enabled = true,
        OperationalMarkerSha256 = new string('a', 64),
        AuthorizedOperation = ServiceClientOperationalOperations.CreateIdentity,
        AuthorizedCommandId = CommandId,
        AuthorizedExpectedOperationalVersion = 0,
        AuthorizedClientCode = "MDM-WORKFLOW-LOCAL",
        AuthorizedServiceName = "Diten.MDM",
        AuthorizedAudience = "TRUSTED_WORKFLOW_CONSUMER"
    };

    private static ServiceClientOperationalProvisioningRequest Request() => new(
        ServiceClientOperationalOperations.CreateIdentity,
        CommandId,
        0,
        null,
        null,
        "MDM-WORKFLOW-LOCAL",
        "Diten.MDM",
        "TRUSTED_WORKFLOW_CONSUMER",
        "1234");

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
