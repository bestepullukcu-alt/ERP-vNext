using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientOperationalProvisioningEligibilityTests
{
    [Theory]
    [InlineData("read-identity")]
    [InlineData("read-grant")]
    [InlineData("rotate-credential")]
    public void Exact_explicit_development_envelope_is_accepted(string operation)
    {
        var request = OperationalTestData.Request(operation);
        OperationalTestData.Eligibility(request).EnsureEligible(request);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("development")]
    [InlineData("")]
    public void Non_exact_development_is_rejected(string environment)
    {
        var request = OperationalTestData.Request();
        Assert.Throws<ServiceClientOperationalContractException>(() =>
            OperationalTestData.Eligibility(request, environment: environment).EnsureEligible(request));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("marker")]
    [InlineData("input-small")]
    [InlineData("input-large")]
    [InlineData("age-zero")]
    [InlineData("age-large")]
    [InlineData("timeout-zero")]
    [InlineData("timeout-large")]
    public void Invalid_configuration_is_fail_closed(string fault)
    {
        var request = OperationalTestData.Request();
        var options = OperationalTestData.Options(request);
        switch (fault)
        {
            case "disabled": options.Enabled = false; break;
            case "marker": options.OperationalMarkerSha256 = "not-a-hash"; break;
            case "input-small": options.MaximumInputBytes = 1023; break;
            case "input-large": options.MaximumInputBytes = 65537; break;
            case "age-zero": options.MaximumOperatorTokenAgeSeconds = 0; break;
            case "age-large": options.MaximumOperatorTokenAgeSeconds = 301; break;
            case "timeout-zero": options.OperationTimeoutSeconds = 0; break;
            case "timeout-large": options.OperationTimeoutSeconds = 121; break;
        }
        Assert.Throws<ServiceClientOperationalContractException>(() =>
            OperationalTestData.Eligibility(request, options).EnsureEligible(request));
    }

    [Theory]
    [InlineData("command")]
    [InlineData("identity")]
    [InlineData("tenant")]
    [InlineData("client")]
    [InlineData("service")]
    [InlineData("audience")]
    [InlineData("version")]
    [InlineData("credential")]
    [InlineData("operation")]
    [InlineData("pipe")]
    public void Envelope_drift_is_rejected(string field)
    {
        var request = OperationalTestData.Request();
        var eligibility = OperationalTestData.Eligibility(request);
        var changed = field switch
        {
            "command" => request with { CommandId = Guid.NewGuid() },
            "identity" => request with { ServiceClientIdentityId = Guid.NewGuid() },
            "tenant" => request with { TenantId = Guid.NewGuid() },
            "client" => request with { ClientCode = "other" },
            "service" => request with { ServiceName = "other" },
            "audience" => request with { Audience = "other" },
            "version" => request with { ExpectedOperationalVersion = 1 },
            "credential" => request with { ExpectedCredentialVersion = "unexpected" },
            "operation" => request with { Operation = "READ-IDENTITY" },
            _ => request with { InheritedPipeHandle = "9" }
        };
        Assert.Throws<ServiceClientOperationalContractException>(() => eligibility.EnsureEligible(changed));
    }

    [Fact]
    public void Options_and_environment_are_frozen_at_construction()
    {
        var request = OperationalTestData.Request();
        var options = OperationalTestData.Options(request);
        var environment = new OperationalTestEnvironment();
        var eligibility = new DevelopmentServiceClientOperationalProvisioningEligibility(environment, Microsoft.Extensions.Options.Options.Create(options));
        options.Enabled = false;
        options.AuthorizedClientCode = "changed";
        environment.EnvironmentName = "Production";
        eligibility.EnsureEligible(request);
        Assert.Throws<ServiceClientOperationalContractException>(() => eligibility.EnsureEligible(request with { ClientCode = "changed" }));
    }
}

internal static class OperationalTestData
{
    internal static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    internal static readonly Guid ActorId = Guid.Parse("1a4b61b6-7e12-44be-9ee8-c7e07e3bf74c");
    internal const string Marker = "test-only-one-shot-operational-marker";
    internal static readonly ServiceClientOperationalActor Actor = new(ActorId, PlatformTenant, "platform_admin");
    internal static ServiceClientOperationalProvisioningRequest Request(string operation = "read-identity") =>
        new(operation, Guid.NewGuid(), 0, Guid.NewGuid(), operation == "read-grant" ? Guid.NewGuid() : null,
            "fixture-client", "Diten.MDM", "TRUSTED_WORKFLOW_CONSUMER", operation == "rotate-credential" ? "123" : null,
            operation == "rotate-credential" ? "v1" : null);
    internal static ServiceClientOperationalProvisioningOptions Options(ServiceClientOperationalProvisioningRequest request) => new()
    {
        Enabled = true, OperationalMarkerSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Marker))),
        AuthorizedOperation = request.Operation, AuthorizedCommandId = request.CommandId,
        AuthorizedExpectedOperationalVersion = request.ExpectedOperationalVersion,
        AuthorizedServiceClientIdentityId = request.ServiceClientIdentityId, AuthorizedTenantId = request.TenantId,
        AuthorizedClientCode = request.ClientCode!, AuthorizedServiceName = request.ServiceName!,
        AuthorizedAudience = request.Audience!, AuthorizedExpectedCredentialVersion = request.ExpectedCredentialVersion
    };
    internal static DevelopmentServiceClientOperationalProvisioningEligibility Eligibility(
        ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalProvisioningOptions? options = null,
        string environment = "Development") => new(new OperationalTestEnvironment { EnvironmentName = environment },
        Microsoft.Extensions.Options.Options.Create(options ?? Options(request)));
}

internal sealed class OperationalTestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "isolated-test";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
