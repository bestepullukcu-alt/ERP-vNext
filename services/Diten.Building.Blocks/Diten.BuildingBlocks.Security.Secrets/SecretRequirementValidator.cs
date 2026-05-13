using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class SecretRequirementValidator : ISecretRequirementValidator
{
    private static readonly string[] WeakPlaceholders =
    [
        "change-me",
        "change-me-in-production",
        "default-secret",
        "local-dev-secret",
        "password",
        "test",
        "admin",
        "123456"
    ];

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly SecretsProviderOptions _options;

    public SecretRequirementValidator(
        IConfiguration configuration,
        IHostEnvironment environment,
        IOptions<SecretsProviderOptions> options)
    {
        _configuration = configuration;
        _environment = environment;
        _options = options.Value;
    }

    public SecretValidationResult Validate(IEnumerable<RequiredSecretDefinition> requirements)
    {
        var errors = new List<string>();

        foreach (var requirement in requirements)
        {
            var enabled = requirement.IsEnabled?.Invoke() ?? true;
            if (!enabled && !requirement.Required)
            {
                continue;
            }

            if (requirement.Kind == SecretRequirementKind.JwtPreviousCollection)
            {
                ValidatePreviousSecrets(requirement, errors);
                continue;
            }

            var value = ResolveSecret(requirement.Key);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (requirement.Required && enabled)
                {
                    errors.Add($"'{requirement.Key}' is required for {requirement.ServiceContext}.");
                }

                continue;
            }

            ValidateValue(requirement, value, errors);
        }

        return errors.Count == 0 ? SecretValidationResult.Success() : SecretValidationResult.Failure(errors);
    }

    private void ValidatePreviousSecrets(RequiredSecretDefinition requirement, ICollection<string> errors)
    {
        var current = ResolveSecret("JwtSettings:Secret");
        var previousSecrets = _configuration.GetSection(requirement.Key)
            .GetChildren()
            .Select(child => child.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        if (previousSecrets.Any(value => string.Equals(value, current, StringComparison.Ordinal)))
        {
            errors.Add($"'{requirement.Key}' contains the current JWT secret.");
        }

        if (previousSecrets.Length != previousSecrets.Distinct(StringComparer.Ordinal).Count())
        {
            errors.Add($"'{requirement.Key}' contains duplicate values.");
        }

        foreach (var value in previousSecrets)
        {
            ValidateValue(requirement with { Key = requirement.Key }, value, errors);
        }
    }

    private void ValidateValue(RequiredSecretDefinition requirement, string value, ICollection<string> errors)
    {
        var trimmed = value.Trim();
        var minimumLength = requirement.MinimumLength ?? requirement.Kind switch
        {
            SecretRequirementKind.JwtCurrent => 32,
            SecretRequirementKind.InternalApiKey => 24,
            _ => 1
        };

        if (trimmed.Length < minimumLength)
        {
            errors.Add($"'{requirement.Key}' must be at least {minimumLength} characters for {requirement.ServiceContext}.");
        }

        if (WeakPlaceholders.Any(placeholder => string.Equals(trimmed, placeholder, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"'{requirement.Key}' uses a forbidden placeholder for {requirement.ServiceContext}.");
        }
    }

    private string ResolveSecret(string key)
    {
        if (_environment.IsProduction() && _options.RequireEnvironmentVariablesInProduction)
        {
            return Environment.GetEnvironmentVariable(ConfigurationSecretsProvider.ToEnvironmentVariableName(key)) ?? string.Empty;
        }

        return _configuration[key] ?? string.Empty;
    }
}
