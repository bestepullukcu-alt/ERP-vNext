using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class ConfigurationSecretsProvider : ISecretsProvider
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly SecretsProviderOptions _options;

    public ConfigurationSecretsProvider(
        IConfiguration configuration,
        IHostEnvironment environment,
        IOptions<SecretsProviderOptions> options)
    {
        _configuration = configuration;
        _environment = environment;
        _options = options.Value;
    }

    public Task<string> GetSecretAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var value = ResolveSecret(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SecretValidationException(
                EffectiveServiceName(),
                [$"Required secret '{key}' is missing or empty."]);
        }

        return Task.FromResult(value);
    }

    public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var section = _configuration.GetSection(prefix);
        var values = section.GetChildren()
            .Select(child => new KeyValuePair<string, string>($"{prefix}:{child.Key}", ResolveSecret($"{prefix}:{child.Key}")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyDictionary<string, string>>(values);
    }

    internal string ResolveSecret(string key)
    {
        if (_environment.IsProduction() && _options.RequireEnvironmentVariablesInProduction)
        {
            return Environment.GetEnvironmentVariable(ToEnvironmentVariableName(key)) ?? string.Empty;
        }

        return _configuration[key] ?? string.Empty;
    }

    private string EffectiveServiceName() =>
        string.IsNullOrWhiteSpace(_options.ServiceName) ? _environment.ApplicationName : _options.ServiceName;

    public static string ToEnvironmentVariableName(string key) => key.Replace(':', '_').Replace("_", "__");
}
