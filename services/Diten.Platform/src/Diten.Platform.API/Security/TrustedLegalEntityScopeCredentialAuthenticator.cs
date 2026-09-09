using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Security;

public sealed class TrustedLegalEntityScopeCredentialAuthenticator : ITrustedLegalEntityScopeCredentialAuthenticator
{
    public const string ConsumerService = "DITENMDMSERVICE";
    public const string Audience = "TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE";
    public const int MaxAllowedPairs = 32;

    private readonly TrustedLegalEntityScopeCredentialBinding _credential;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<(string ModuleCode, string PermissionKey)> _allowedPairs;
    private readonly bool _configurationValid;

    public TrustedLegalEntityScopeCredentialAuthenticator(
        IOptions<TrustedLegalEntityScopeCredentialOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _credential = options.Value.Mdm;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _allowedPairs = _credential.AllowedPairs
            .Select(pair => (pair.ModuleCode, pair.PermissionKey))
            .ToHashSet();
        _configurationValid = IsValidConfiguration(_credential, _allowedPairs);
    }

    private static readonly HashSet<string> FirstConsumerPermissions = new(StringComparer.Ordinal)
    {
        "mdm.global-products.read", "mdm.global-products.create", "mdm.gskus.read", "mdm.gskus.create",
        "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
        "mdm.gskus.request-correction", "mdm.gskus.request-retirement", "mdm.gskus.retire",
        "mdm.lskus.read", "mdm.lskus.create", "mdm.finished-goods.read", "mdm.finished-goods.create",
        "mdm.lskus.withdraw", "mdm.lskus.request-retirement",
        "mdm.lskus.submit", "mdm.lskus.retire",
        "mdm.product-abbreviations.read", "mdm.product-abbreviations.request",
        "mdm.product-abbreviations.approve", "mdm.product-abbreviations.reject",
        "mdm.product-abbreviations.correct", "mdm.product-abbreviations.cancel",
        "mdm.product-abbreviations.retire", "mdm.product-abbreviations.audit",
        "mdm.product-legal-entity-scopes.read", "mdm.product-legal-entity-scopes.configure",
        "mdm.product-legal-entity-scopes.replace", "mdm.product-legal-entity-scopes.end",
        "mdm.product-legal-entity-scope-rollout.activate", "mdm.product-legal-entity-scope-rollout.rollback"
    };

    public TrustedLegalEntityScopeCredentialResult Authenticate(
        string? identifier,
        string? secret,
        string? audience)
    {
        if (!_configurationValid)
        {
            return TrustedLegalEntityScopeCredentialResult.Denied;
        }

        if (_credential.IsRevoked
            || string.IsNullOrWhiteSpace(identifier)
            || string.IsNullOrEmpty(secret)
            || !string.Equals(identifier, _credential.Identifier, StringComparison.Ordinal))
        {
            return TrustedLegalEntityScopeCredentialResult.Unauthenticated;
        }

        if (!string.Equals(_credential.ConsumerService, ConsumerService, StringComparison.Ordinal)
            || !string.Equals(_credential.AllowedAudience, Audience, StringComparison.Ordinal)
            || !string.Equals(audience, Audience, StringComparison.Ordinal))
        {
            return TrustedLegalEntityScopeCredentialResult.Denied;
        }

        var validSecret = FixedTimeEquals(secret, _credential.ActiveSecret);
        if (!validSecret
            && !string.IsNullOrEmpty(_credential.PreviousSecret)
            && _credential.PreviousValidUntilUtc.HasValue
            && _timeProvider.GetUtcNow() < _credential.PreviousValidUntilUtc.Value)
        {
            validSecret = FixedTimeEquals(secret, _credential.PreviousSecret);
        }

        return validSecret
            ? new TrustedLegalEntityScopeCredentialResult(true, false)
            : TrustedLegalEntityScopeCredentialResult.Unauthenticated;
    }

    public bool AllowsPair(string moduleCode, string permissionKey) =>
        _configurationValid
        && string.Equals(moduleCode, "product-item-sku-master", StringComparison.Ordinal)
        && FirstConsumerPermissions.Contains(permissionKey)
        && _allowedPairs.Contains((moduleCode, permissionKey));

    private static bool IsValidConfiguration(
        TrustedLegalEntityScopeCredentialBinding credential,
        IReadOnlySet<(string ModuleCode, string PermissionKey)> distinctPairs)
    {
        var configuredPairs = credential.AllowedPairs;
        return configuredPairs.Count is > 0 and <= MaxAllowedPairs
            && distinctPairs.Count == configuredPairs.Count
            && configuredPairs.All(pair =>
                string.Equals(pair.ModuleCode, "product-item-sku-master", StringComparison.Ordinal)
                && TrustedLegalEntityScopeResolutionLimits.IsValidModuleCode(pair.ModuleCode)
                && TrustedLegalEntityScopeResolutionLimits.IsValidPermissionKey(pair.PermissionKey)
                && FirstConsumerPermissions.Contains(pair.PermissionKey));
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
