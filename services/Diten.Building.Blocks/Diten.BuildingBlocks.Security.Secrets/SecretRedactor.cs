using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class SecretRedactor : ISecretRedactor
{
    private readonly SecretRedactionOptions _options;

    public SecretRedactor(IOptions<SecretRedactionOptions> options)
    {
        _options = options.Value;
    }

    public string Redact(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return IsSensitiveKey(key) ? _options.Mask : value;
    }

    private bool IsSensitiveKey(string key) =>
        _options.SensitiveKeyTokens.Any(token => key.Contains(token, StringComparison.OrdinalIgnoreCase));
}
