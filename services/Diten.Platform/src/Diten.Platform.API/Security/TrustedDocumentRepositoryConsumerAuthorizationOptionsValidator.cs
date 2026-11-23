using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0262-FU01 DL-2 (r4, F-2) — start-up validation of the closed consumer list (<c>ValidateOnStart</c>; emsal
/// <c>TrustedWorkflowStartAuthorizationOptionsValidator</c>). Every entry must match the set FIXED IN CODE below:
/// service <c>Diten.MDM</c>, audience <c>TRUSTED_DOCUMENT_REPOSITORY_CONSUMER</c>, a module of
/// <see cref="AllowedScopesByModule"/> and a non-empty subset of that module's scopes, a non-empty client id. A second entry
/// for the same module or the same client id, more than eight entries, or a general file limit
/// (<c>ContentStorageOptions.MaxFileSizeBytes</c>) below the trusted limit → the application does not start.
/// <para>Adding a consumer is a pack revision + review + approval row + this code set + an Auth client (§8.1 "Listeye
/// ekleme kuralı"); writing configuration is never enough.</para>
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator :
    IValidateOptions<TrustedDocumentRepositoryConsumerAuthorizationOptions>
{
    public const string ConfigurationError = "DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_CONFIGURATION_INVALID";

    /// <summary>v1: only MOD-0217 (CLM), only the <c>Documents</c> scope (KARAR D56/2, S-4).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedScopesByModule =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["MOD-0217"] = new HashSet<string>(StringComparer.Ordinal) { nameof(ContentStorageScope.Documents) }
        };

    private readonly IOptions<ContentStorageOptions>? _storage;

    public TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator(IOptions<ContentStorageOptions>? storage = null)
    {
        _storage = storage;
    }

    public ValidateOptionsResult Validate(string? name, TrustedDocumentRepositoryConsumerAuthorizationOptions options)
    {
        var storage = _storage?.Value ?? new ContentStorageOptions();
        if (storage.MaxFileSizeBytes < TrustedDocumentRepositoryUploadLimits.MaxFileBytes)
        {
            return ValidateOptionsResult.Fail(ConfigurationError);
        }

        return ValidateValue(options);
    }

    internal static ValidateOptionsResult ValidateValue(TrustedDocumentRepositoryConsumerAuthorizationOptions? options)
    {
        if (options?.Entries is null
            || options.Entries.Count > TrustedDocumentRepositoryConsumerAuthorizationOptions.MaximumEntries)
        {
            return ValidateOptionsResult.Fail(ConfigurationError);
        }

        var modules = new HashSet<string>(StringComparer.Ordinal);
        var clients = new HashSet<Guid>();
        foreach (var entry in options.Entries)
        {
            if (!IsValid(entry) || !modules.Add(entry.ConsumerModule) || !clients.Add(entry.ClientId))
            {
                return ValidateOptionsResult.Fail(ConfigurationError);
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValid(TrustedDocumentRepositoryConsumerEntry? entry) =>
        entry is not null
        && entry.ClientId != Guid.Empty
        && string.Equals(entry.ServiceName, TrustedServiceTokenValidationExtensions.RequiredServiceName, StringComparison.Ordinal)
        && string.Equals(entry.Audience, TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience, StringComparison.Ordinal)
        && AllowedScopesByModule.TryGetValue(entry.ConsumerModule ?? string.Empty, out var allowedScopes)
        && entry.Scopes is { Count: > 0 }
        && entry.Scopes.All(scope => scope is not null && allowedScopes.Contains(scope))
        && entry.Scopes.Distinct(StringComparer.Ordinal).Count() == entry.Scopes.Count;
}
