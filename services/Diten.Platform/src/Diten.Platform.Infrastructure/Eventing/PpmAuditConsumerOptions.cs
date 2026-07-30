using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class PpmAuditConsumerOptions
{
    public const string SectionName = "PpmAuditConsumer";

    public bool Enabled { get; set; }
    public string ActiveKeyId { get; set; } = string.Empty;
    public string ActiveSecret { get; set; } = string.Empty;
    public string? PreviousKeyId { get; set; }
    public string? PreviousSecret { get; set; }
}

internal sealed class PpmAuditConsumerOptionsValidator : IValidateOptions<PpmAuditConsumerOptions>
{
    public ValidateOptionsResult Validate(string? name, PpmAuditConsumerOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        ValidateKey("active", options.ActiveKeyId, options.ActiveSecret, required: true, failures);
        var hasPrevious = !string.IsNullOrWhiteSpace(options.PreviousKeyId)
                          || !string.IsNullOrWhiteSpace(options.PreviousSecret);
        ValidateKey("previous", options.PreviousKeyId, options.PreviousSecret, hasPrevious, failures);

        if (hasPrevious && string.Equals(options.ActiveKeyId, options.PreviousKeyId, StringComparison.Ordinal))
        {
            failures.Add("PpmAuditConsumer previous key id must differ from the active key id.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateKey(
        string label,
        string? keyId,
        string? encodedSecret,
        bool required,
        ICollection<string> failures)
    {
        if (!required)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 64)
        {
            failures.Add($"PpmAuditConsumer {label} key id is required and must not exceed 64 characters.");
        }

        if (string.IsNullOrWhiteSpace(encodedSecret)
            || encodedSecret.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
            || encodedSecret.Contains("change-me", StringComparison.OrdinalIgnoreCase)
            || !TryDecodeSecret(encodedSecret, out var secret)
            || secret.Length < 32)
        {
            failures.Add($"PpmAuditConsumer {label} secret must be Base64 encoded and contain at least 32 bytes.");
        }
    }

    internal static bool TryDecodeSecret(string encodedSecret, out byte[] secret)
    {
        try
        {
            secret = Convert.FromBase64String(encodedSecret);
            return true;
        }
        catch (FormatException)
        {
            secret = [];
            return false;
        }
    }
}
