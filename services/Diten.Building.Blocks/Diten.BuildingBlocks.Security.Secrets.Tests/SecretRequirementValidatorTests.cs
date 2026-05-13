using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.BuildingBlocks.Security.Secrets.Tests;

public sealed class SecretRequirementValidatorTests
{
    [Fact]
    public void SecretRequirementValidator_MissingRequiredSecret_FailsStartupSafely()
    {
        var validator = CreateValidator(new Dictionary<string, string?>(), "Development");

        var result = validator.Validate([
            new RequiredSecretDefinition("JwtSettings:Secret", "UnitTest", SecretRequirementKind.JwtCurrent)
        ]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("JwtSettings:Secret", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Errors, error => error.Contains("value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SecretRequirementValidator_PlaceholderSecret_IsRejected()
    {
        var validator = CreateValidator(new Dictionary<string, string?>
        {
            ["AuthService:InternalApiKey"] = "change-me"
        }, "Development");

        var result = validator.Validate([
            new RequiredSecretDefinition("AuthService:InternalApiKey", "UnitTest", SecretRequirementKind.InternalApiKey)
        ]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("forbidden placeholder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SecretRequirementValidator_MfaDisabledMissingHashSecret_IsAccepted()
    {
        var validator = CreateValidator(new Dictionary<string, string?>(), "Development");

        var result = validator.Validate([
            new RequiredSecretDefinition("Mfa:HashSecret", "UnitTest", MinimumLength: 32, Required: false, IsEnabled: () => false)
        ]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void JwtSecretRotationResolver_DuplicatePreviousSecret_IsRejected()
    {
        var validator = CreateValidator(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "CurrentJwtSigningSecretForUnitTests12345",
            ["JwtSettings:PreviousSecrets:0"] = "PreviousJwtSigningSecretForUnitTests123",
            ["JwtSettings:PreviousSecrets:1"] = "PreviousJwtSigningSecretForUnitTests123"
        }, "Development");

        var result = validator.Validate([
            new RequiredSecretDefinition("JwtSettings:Secret", "UnitTest", SecretRequirementKind.JwtCurrent),
            new RequiredSecretDefinition("JwtSettings:PreviousSecrets", "UnitTest", SecretRequirementKind.JwtPreviousCollection, Required: false)
        ]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SecretRedactor_MasksSensitiveKeys_InLogsAndErrors()
    {
        var redactor = new SecretRedactor(Options.Create(new SecretRedactionOptions()));

        var redacted = redactor.Redact("JwtSettings:Secret", "CurrentJwtSigningSecretForUnitTests12345");

        Assert.Equal("***REDACTED***", redacted);
    }

    private static SecretRequirementValidator CreateValidator(Dictionary<string, string?> values, string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new SecretRequirementValidator(
            configuration,
            new TestHostEnvironment(environmentName),
            Options.Create(new SecretsProviderOptions
            {
                ServiceName = "UnitTest",
                RequireEnvironmentVariablesInProduction = true
            }));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "UnitTest";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
