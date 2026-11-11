using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0040-FU02 §8.2 — the consumer credential of the record-code allocate endpoint (P2, the MOD-0018-FU21 pattern). The
/// binding → service-name map is code and closed: a configuration can narrow a binding's record types, never widen them,
/// rename a service or add a consumer. A configuration outside that map is invalid and every request is refused 403.
/// </summary>
public sealed class TrustedRecordCodeCredentialAuthenticator : ITrustedRecordCodeCredentialAuthenticator
{
    public const string Audience = "TRUSTED_RECORD_CODE_ALLOCATE";

    /// <summary>The closed consumer list (pack §8.2): binding, consumer-service claim, audit source service, record types.</summary>
    public static readonly IReadOnlyList<TrustedRecordCodeConsumer> Consumers =
    [
        new("FinanceTreasury", "DITENFINANCETREASURYSERVICE", "Diten.FinanceTreasuryService", ["PAY", "APD", "ARI", "CPS", "CTP"]),
        new("Mdm", "DITENMDMSERVICE", "Diten.MdmService", ["BNK", "BAC", "AGR"])
    ];

    private readonly IReadOnlyList<(TrustedRecordCodeConsumer Consumer, TrustedRecordCodeCredentialBinding Binding)> _bindings;
    private readonly TimeProvider _timeProvider;
    private readonly bool _configurationValid;

    public TrustedRecordCodeCredentialAuthenticator(
        IOptions<TrustedRecordCodeCredentialOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _bindings =
        [
            (Consumers[0], options.Value.FinanceTreasury ?? new TrustedRecordCodeCredentialBinding()),
            (Consumers[1], options.Value.Mdm ?? new TrustedRecordCodeCredentialBinding())
        ];
        _configurationValid = IsValidConfiguration(_bindings);
    }

    public TrustedRecordCodeCredentialResult Authenticate(string? identifier, string? secret, string? audience)
    {
        if (!_configurationValid)
        {
            return TrustedRecordCodeCredentialResult.Denied;
        }

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(secret))
        {
            return TrustedRecordCodeCredentialResult.Unauthenticated;
        }

        var match = _bindings.FirstOrDefault(pair =>
            IsConfigured(pair.Binding)
            && string.Equals(identifier, pair.Binding.Identifier, StringComparison.Ordinal));
        if (match.Binding is null || match.Binding.IsRevoked)
        {
            return TrustedRecordCodeCredentialResult.Unauthenticated;
        }

        var (consumer, binding) = match;
        if (!string.Equals(binding.ConsumerService, consumer.ConsumerService, StringComparison.Ordinal)
            || !string.Equals(binding.AllowedAudience, Audience, StringComparison.Ordinal)
            || !string.Equals(audience, Audience, StringComparison.Ordinal))
        {
            return TrustedRecordCodeCredentialResult.Denied;
        }

        var validSecret = FixedTimeEquals(secret, binding.ActiveSecret);
        if (!validSecret
            && !string.IsNullOrEmpty(binding.PreviousSecret)
            && binding.PreviousValidUntilUtc.HasValue
            && _timeProvider.GetUtcNow() < binding.PreviousValidUntilUtc.Value)
        {
            validSecret = FixedTimeEquals(secret, binding.PreviousSecret);
        }

        return validSecret
            ? new TrustedRecordCodeCredentialResult(
                true,
                false,
                consumer.ConsumerService,
                consumer.SourceService,
                binding.AllowedRecordTypes.ToHashSet(StringComparer.Ordinal))
            : TrustedRecordCodeCredentialResult.Unauthenticated;
    }

    private static bool IsConfigured(TrustedRecordCodeCredentialBinding binding) =>
        !string.IsNullOrWhiteSpace(binding.Identifier);

    private static bool IsValidConfiguration(
        IReadOnlyList<(TrustedRecordCodeConsumer Consumer, TrustedRecordCodeCredentialBinding Binding)> bindings)
    {
        var configured = bindings.Where(pair => IsConfigured(pair.Binding)).ToArray();
        if (configured.Length == 0
            || configured.Select(pair => pair.Binding.Identifier).Distinct(StringComparer.Ordinal).Count() != configured.Length)
        {
            return false;
        }

        return configured.All(pair =>
        {
            var types = pair.Binding.AllowedRecordTypes ?? [];
            return !string.IsNullOrEmpty(pair.Binding.ActiveSecret)
                && types.Count > 0
                && types.Distinct(StringComparer.Ordinal).Count() == types.Count
                && types.All(type => pair.Consumer.RecordTypes.Contains(type, StringComparer.Ordinal));
        });
    }

    private static bool FixedTimeEquals(string provided, string? expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}

public sealed record TrustedRecordCodeConsumer(
    string Binding,
    string ConsumerService,
    string SourceService,
    IReadOnlyList<string> RecordTypes);
