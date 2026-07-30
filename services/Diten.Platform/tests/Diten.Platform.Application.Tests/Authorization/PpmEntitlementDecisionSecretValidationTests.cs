using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class PpmEntitlementDecisionSecretValidationTests
{
    [Fact]
    public void DisabledWithMissingPpmCredentialPassesPlatformSecretValidation()
    {
        var configuration = CreateConfiguration(enabled: false, credential: null);

        var exception = Record.Exception(() => Validate(configuration));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null, "is required")]
    [InlineData("too-short", "at least 24 characters")]
    [InlineData("change-me", "forbidden placeholder")]
    public void EnabledWithMissingShortOrPlaceholderCredentialFailsStartup(
        string? credential,
        string expectedError)
    {
        var configuration = CreateConfiguration(enabled: true, credential);

        var exception = Assert.Throws<SecretValidationException>(() => Validate(configuration));

        Assert.Contains(exception.Errors, error => error.Contains(expectedError, StringComparison.Ordinal));
    }

    [Fact]
    public void EnabledWithValidDedicatedCredentialPassesPlatformSecretValidation()
    {
        var configuration = CreateConfiguration(
            enabled: true,
            credential: "valid-dedicated-ppm-service-key");

        var exception = Record.Exception(() => Validate(configuration));

        Assert.Null(exception);
    }

    private static void Validate(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        var environment = new TestHostEnvironment();
        services.ValidateRequiredSecrets(
            configuration,
            environment,
            "Platform",
            Diten.Platform.Infrastructure.DependencyInjection.BuildSecretRequirements(configuration));
    }

    private static IConfiguration CreateConfiguration(bool enabled, string? credential)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "valid-jwt-secret-at-least-thirty-two-characters",
                ["MongoDbSettings:ConnectionString"] = "mongodb://localhost:27017",
                ["AuthService:InternalApiKey"] = "valid-auth-internal-api-key",
                ["Smtp:Enabled"] = "false",
                ["PpmEntitlementDecision:Enabled"] = enabled.ToString(),
                ["PpmEntitlementDecision:ServiceCredential"] = credential
            })
            .Build();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Diten.Platform.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
