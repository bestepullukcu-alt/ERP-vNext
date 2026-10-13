using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.InternalAccess;

/// <summary>The answer for one presented key: who it is (a CONFIGURED identifier, or null) and whether it opens the operation.</summary>
public readonly record struct InternalKeyCheck(bool Allowed, string? KeyIdentifier);

/// <summary>
/// Checks <c>X-Internal-Api-Key: &lt;identifier&gt;:&lt;secret&gt;</c> against <see cref="InternalServiceKeyOptions"/>
/// (WP-0173-PM2 security conditions):
/// <list type="bullet">
/// <item>Constant time: identifier and secret are each compared as SHA-256 digests with
/// <see cref="CryptographicOperations.FixedTimeEquals"/> — equal-length inputs, so neither the length nor the first
/// differing byte leaks — and EVERY configured key and secret is compared on every call (no early exit).</item>
/// <item>A key opens an operation only when its configured <see cref="InternalServiceKeyOptions.Endpoints"/> name it.</item>
/// <item>The configuration is read on every call (user secrets / environment changes and rotation apply without code);
/// the presented secret is hashed and dropped, never stored, logged or returned.</item>
/// </list>
/// </summary>
public sealed class InternalServiceKeyAuthenticator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalServiceKeyAuthenticator> _logger;

    public InternalServiceKeyAuthenticator(IConfiguration configuration, ILogger<InternalServiceKeyAuthenticator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public InternalKeyCheck Check(string? presentedHeader, string operationId)
    {
        var keys = ValidKeys();
        var separator = presentedHeader?.IndexOf(':') ?? -1;
        var presentedId = separator > 0 ? presentedHeader![..separator] : string.Empty;
        var presentedSecret = separator > 0 ? presentedHeader![(separator + 1)..] : string.Empty;
        var idHash = Sha256(presentedId);
        var secretHash = Sha256(presentedSecret);

        string? matched = null;
        var allowed = false;
        foreach (var key in keys)
        {
            var idMatch = CryptographicOperations.FixedTimeEquals(idHash, Sha256(key.Identifier));
            var secretMatch = false;
            foreach (var secret in key.Secrets)
            {
                secretMatch |= CryptographicOperations.FixedTimeEquals(secretHash, Sha256(secret));
            }

            if (idMatch & secretMatch & separator > 0)
            {
                matched = key.Identifier;
                allowed = key.Endpoints.Contains(operationId, StringComparer.Ordinal);
            }
        }

        return new InternalKeyCheck(allowed, matched);
    }

    /// <summary>The configured keys that obey every rule; the others are skipped (logged by identifier, never by secret).</summary>
    private List<InternalServiceKeyOptions> ValidKeys()
    {
        var configured = _configuration.GetSection(InternalServiceKeyOptions.SectionName).Get<List<InternalServiceKeyOptions>>() ?? [];
        var valid = new List<InternalServiceKeyOptions>(configured.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in configured)
        {
            var secrets = key.Secrets.Where(s => !string.IsNullOrEmpty(s)).ToList();
            var reason =
                string.IsNullOrWhiteSpace(key.Identifier) || key.Identifier.Contains(':') ? "the identifier is empty or contains ':'"
                : !seen.Add(key.Identifier) ? "the identifier is configured twice"
                : secrets.Count == 0 ? "no secret is configured"
                : secrets.Count > InternalServiceKeyOptions.MaxSecrets ? $"more than {InternalServiceKeyOptions.MaxSecrets} secrets are configured"
                : secrets.Any(s => s.Length < InternalServiceKeyOptions.MinSecretLength) ? $"a secret is shorter than {InternalServiceKeyOptions.MinSecretLength} characters"
                : key.Endpoints.Count == 0 ? "no endpoint is listed"
                : null;
            if (reason is not null)
            {
                _logger.LogError("Internal service key '{KeyIdentifier}' is ignored: {Reason}.", key.Identifier, reason);
                continue;
            }

            valid.Add(new InternalServiceKeyOptions { Identifier = key.Identifier, Secrets = secrets, Endpoints = key.Endpoints });
        }

        return valid;
    }

    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
