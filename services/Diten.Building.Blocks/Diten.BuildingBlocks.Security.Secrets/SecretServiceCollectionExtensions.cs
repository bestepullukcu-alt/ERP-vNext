using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.Security.Secrets;

public static class SecretServiceCollectionExtensions
{
    public static IServiceCollection AddSecretsProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<SecretsProviderOptions>? configure = null)
    {
        services.Configure<SecretsProviderOptions>(options =>
        {
            options.ServiceName = environment.ApplicationName;
            configure?.Invoke(options);
        });
        services.Configure<SecretRedactionOptions>(configuration.GetSection(nameof(SecretRedactionOptions)));
        services.AddSingleton(environment);
        services.AddSingleton<ISecretsProvider, ConfigurationSecretsProvider>();
        services.AddSingleton<ISecretRequirementValidator, SecretRequirementValidator>();
        services.AddSingleton<ISecretRedactor, SecretRedactor>();
        services.AddSingleton<ISecretRotationResolver, JwtSecretRotationResolver>();

        return services;
    }

    public static void ValidateRequiredSecrets(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceContext,
        IEnumerable<RequiredSecretDefinition> requirements)
    {
        var options = Options.Create(new SecretsProviderOptions
        {
            ServiceName = serviceContext,
            RequireEnvironmentVariablesInProduction = true
        });
        var validator = new SecretRequirementValidator(configuration, environment, options);
        var result = validator.Validate(requirements);
        if (!result.Succeeded)
        {
            throw new SecretValidationException(serviceContext, result.Errors);
        }
    }
}
