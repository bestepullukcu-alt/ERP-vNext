using Diten.BuildingBlocks.Security.Secrets;
using Diten.Platform.Infrastructure.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class PpmAuditConsumerSecretValidationTests
{
    [Fact]
    public void DisabledWithMissingPublisherSecretPassesStartupValidation() =>
        Assert.Null(Record.Exception(() => Validate(Create(false, null))));

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("change-me-placeholder-change-me-placeholder-change-me")]
    public void EnabledWithInvalidPublisherSecretFailsStartupValidation(string? secret) =>
        Assert.ThrowsAny<Exception>(() => Validate(Create(true, secret)));

    [Fact]
    public void EnabledWithDedicatedPublisherSecretPassesStartupValidation() =>
        Assert.Null(Record.Exception(() => Validate(Create(
            true,
            "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="))));

    private static void Validate(IConfiguration configuration)
    {
        new ServiceCollection().ValidateRequiredSecrets(
            configuration,
            new TestHostEnvironment(),
            "Platform",
            Diten.Platform.Infrastructure.DependencyInjection.BuildSecretRequirements(configuration));
        var options = configuration.GetSection(PpmAuditConsumerOptions.SectionName).Get<PpmAuditConsumerOptions>()
                      ?? new PpmAuditConsumerOptions();
        var result = new PpmAuditConsumerOptionsValidator().Validate(null, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(result.Failures);
        }
    }

    private static IConfiguration Create(bool enabled, string? secret) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "valid-jwt-secret-at-least-thirty-two-characters",
            ["MongoDbSettings:ConnectionString"] = "mongodb://localhost:27017",
            ["AuthService:InternalApiKey"] = "valid-auth-internal-api-key",
            ["Smtp:Enabled"] = "false",
            ["PpmEntitlementDecision:Enabled"] = "false",
            ["PpmAuditConsumer:Enabled"] = enabled.ToString(),
            ["PpmAuditConsumer:ActiveKeyId"] = "current",
            ["PpmAuditConsumer:ActiveSecret"] = secret
        }).Build();

    private sealed class OptionsValidationException(IEnumerable<string> failures)
        : Exception(string.Join("; ", failures));

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Diten.Platform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
