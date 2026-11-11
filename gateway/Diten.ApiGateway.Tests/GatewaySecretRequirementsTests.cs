using Diten.ApiGateway.Authentication;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// Q151 D2 hardening (owner decision 2026-10-18): outside Development the gateway refuses to start without
/// Observability:Correlation:SigningKey, because without it every SupplyChain lifecycle step after create fails
/// through the gateway. Measured against the production list Program.cs validates (GatewaySecretRequirements.For),
/// so removing or weakening the requirement turns these red.
/// </summary>
public sealed class GatewaySecretRequirementsTests
{
    private static readonly string Jwt = new('j', 48);

    private static SecretValidationResult Validate(string environmentName, string? signingKey)
    {
        var values = new Dictionary<string, string?> { ["JwtSettings:Secret"] = Jwt };
        if (signingKey is not null) values[GatewaySecretRequirements.CorrelationSigningKey] = signingKey;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = new StubEnvironment(environmentName);
        var validator = new SecretRequirementValidator(configuration, environment,
            Options.Create(new SecretsProviderOptions { ServiceName = "ApiGateway" }));
        return validator.Validate(GatewaySecretRequirements.For(environment));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void Outside_Development_a_missing_signing_key_fails_startup_validation(string environment)
    {
        var result = Validate(environment, signingKey: null);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains(GatewaySecretRequirements.CorrelationSigningKey));
    }

    [Fact]
    public void Outside_Development_a_key_shorter_than_32_characters_fails()
    {
        var result = Validate("Staging", signingKey: "too-short-key");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains(GatewaySecretRequirements.CorrelationSigningKey));
    }

    [Fact]
    public void Outside_Development_a_strong_key_passes()
    {
        Assert.True(Validate("Staging", signingKey: new string('k', 64)).Succeeded);
    }

    [Fact]
    public void In_Development_a_missing_key_does_not_block_startup()
    {
        Assert.True(Validate(Environments.Development, signingKey: null).Succeeded);
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.ApiGateway";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
