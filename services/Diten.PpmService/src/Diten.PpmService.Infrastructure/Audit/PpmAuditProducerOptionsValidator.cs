using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Audit;


public sealed class PpmAuditProducerOptionsValidator : IValidateOptions<PpmAuditProducerOptions>
{
    private const string PublicFixtureSecret =
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private static readonly HashSet<string> KnownNonProductionSecrets =
        new(StringComparer.Ordinal)
        {
            PublicFixtureSecret,
            "changeme",
            "change-me",
            "placeholder",
            "default",
            "secret",
            "test",
            "Y2hhbmdlbWU=",
            "Y2hhbmdlLW1l",
            "cGxhY2Vob2xkZXI=",
            "ZGVmYXVsdA==",
            "c2VjcmV0",
            "dGVzdA=="
        };

    public ValidateOptionsResult Validate(string? name, PpmAuditProducerOptions options)
    {
        if (!options.Enabled)
        {
            return options.WorkerEnabled
                ? ValidateOptionsResult.Fail("PpmAuditProducer worker cannot be enabled while the producer is disabled.")
                : ValidateOptionsResult.Success;
        }

        if (!ValidKeyId(options.KeyId))
        {
            return ValidateOptionsResult.Fail("PpmAuditProducer KeyId is invalid.");
        }

        if (!TryDecodeSecret(options.SecretBase64, out var secret)
            || secret.Length < 32
            || KnownNonProductionSecrets.Contains(options.SecretBase64!)
            || IsSingleRepeatedByte(secret))
        {
            return ValidateOptionsResult.Fail("PpmAuditProducer signing secret is invalid.");
        }

        if (options.WorkerEnabled
            && (options.PollIntervalSeconds is < 1 or > 300
            || options.BatchSize is < 1 or > 500
            || options.MaxAttempts is < 1 or > 100
            || options.InitialRetryDelaySeconds is < 1 or > 300
            || options.MaximumRetryDelaySeconds < options.InitialRetryDelaySeconds
            || options.MaximumRetryDelaySeconds > 3600
            || options.PublishingStaleAfterSeconds is < 1 or > 3600))
        {
            return ValidateOptionsResult.Fail("PpmAuditProducer worker settings are invalid.");
        }

        if (options.WorkerEnabled
            && (string.IsNullOrWhiteSpace(options.RabbitMqHost)
            || string.IsNullOrWhiteSpace(options.RabbitMqUsername)
            || string.IsNullOrWhiteSpace(options.RabbitMqPassword)))
        {
            return ValidateOptionsResult.Fail("PpmAuditProducer RabbitMQ transport is not configured.");
        }

        return ValidateOptionsResult.Success;
    }

    public static bool TryDecodeSecret(string? encoded, out byte[] secret)
    {
        secret = [];
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            secret = Convert.FromBase64String(encoded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsSingleRepeatedByte(byte[] secret) =>
        secret.Length > 0 && secret.All(value => value == secret[0]);

    private static bool ValidKeyId(string? keyId) =>
        !string.IsNullOrWhiteSpace(keyId)
        && keyId.Length <= 128
        && keyId.All(character =>
            character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.'
                or '-'
                or '_');
}
