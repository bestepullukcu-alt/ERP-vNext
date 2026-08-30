using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class TrustedLegalEntityScopeCredentialAuthenticatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_and_unexpired_previous_secret_authenticate_exact_consumer_audience_and_pair()
    {
        var authenticator = Create();

        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.True(authenticator.Authenticate("mdm", "previous-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.True(authenticator.AllowsPair("product-item-sku-master", "mdm.gskus.read"));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "MDM.GSKUS.READ"));
    }

    [Fact]
    public void Exact_canonical_30_pair_configuration_accepts_every_pair_and_no_other_pair()
    {
        var authenticator = Create();

        Assert.Equal(30, CanonicalPermissions.Length);
        Assert.All(CanonicalPermissions, permission =>
            Assert.True(authenticator.AllowsPair("product-item-sku-master", permission)));
        Assert.False(authenticator.AllowsPair("other-module", CanonicalPermissions[0]));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.unknown.read"));
    }

    [Fact]
    public void Revocation_expiry_wrong_audience_and_wrong_service_fail_closed()
    {
        var revoked = Options(); revoked.Mdm.IsRevoked = true;
        Assert.False(Create(revoked).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        var expired = Options(); expired.Mdm.PreviousValidUntilUtc = Now;
        Assert.False(Create(expired).Authenticate("mdm", "previous-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.True(Create().Authenticate("mdm", "active-secret", "wrong").Forbidden);
        var wrongService = Options(); wrongService.Mdm.ConsumerService = "OTHER";
        Assert.True(Create(wrongService).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("invalid-module")]
    [InlineData("invalid-permission")]
    [InlineData("unknown")]
    [InlineData("empty")]
    public void Invalid_allowed_pair_configuration_fails_before_serving(string variant)
    {
        var options = Options();
        var pair = options.Mdm.AllowedPairs[0];
        switch (variant)
        {
            case "duplicate": options.Mdm.AllowedPairs.Add(new() { ModuleCode = pair.ModuleCode, PermissionKey = pair.PermissionKey }); break;
            case "invalid-module": pair.ModuleCode = "Product_Item"; break;
            case "invalid-permission": pair.PermissionKey = "mdm..read"; break;
            case "unknown": pair.PermissionKey = "mdm.unknown.read"; break;
            case "empty": options.Mdm.AllowedPairs.Clear(); break;
        }

        var authenticator = Create(options);

        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.gskus.read"));
    }

    [Fact]
    public void More_than_exact_maximum_pairs_fails_closed()
    {
        var options = Options();
        options.Mdm.AllowedPairs = Enumerable.Range(0, 31)
            .Select(i => new TrustedLegalEntityScopeAllowedPair { ModuleCode = "product-item-sku-master", PermissionKey = $"mdm.x{i}.read" })
            .ToList();

        Assert.True(Create(options).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
    }

    [Fact]
    public void Canonical_configuration_subset_authenticates_and_allows_only_configured_pairs()
    {
        var options = Options();
        var configuredPermission = options.Mdm.AllowedPairs[0].PermissionKey;
        var omittedPermission = options.Mdm.AllowedPairs[^1].PermissionKey;
        options.Mdm.AllowedPairs = [options.Mdm.AllowedPairs[0]];
        var authenticator = Create(options);

        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.True(authenticator.AllowsPair("product-item-sku-master", configuredPermission));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", omittedPermission));
    }

    private static TrustedLegalEntityScopeCredentialAuthenticator Create(TrustedLegalEntityScopeCredentialOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? Options()), new FixedTimeProvider(Now));

    private static TrustedLegalEntityScopeCredentialOptions Options() => new()
    {
        Mdm = new TrustedLegalEntityScopeCredentialBinding
        {
            Identifier = "mdm", ActiveSecret = "active-secret", PreviousSecret = "previous-secret",
            PreviousValidUntilUtc = Now.AddMinutes(1), ConsumerService = TrustedLegalEntityScopeCredentialAuthenticator.ConsumerService,
            AllowedAudience = TrustedLegalEntityScopeCredentialAuthenticator.Audience,
            AllowedPairs = CanonicalPermissions.Select(permission => new TrustedLegalEntityScopeAllowedPair
            { ModuleCode = "product-item-sku-master", PermissionKey = permission }).ToList()
        }
    };

    private static readonly string[] CanonicalPermissions =
    [
        "mdm.global-products.read", "mdm.global-products.create", "mdm.gskus.read", "mdm.gskus.create",
        "mdm.lskus.read", "mdm.lskus.create", "mdm.finished-goods.read", "mdm.finished-goods.create",
        "mdm.global-products.submit", "mdm.global-products.retire",
        "mdm.gskus.submit", "mdm.gskus.retire",
        "mdm.lskus.submit", "mdm.lskus.retire",
        "mdm.finished-goods.submit", "mdm.finished-goods.retire",
        "mdm.product-abbreviations.read", "mdm.product-abbreviations.request",
        "mdm.product-abbreviations.approve", "mdm.product-abbreviations.reject",
        "mdm.product-abbreviations.correct", "mdm.product-abbreviations.cancel",
        "mdm.product-abbreviations.retire", "mdm.product-abbreviations.audit",
        "mdm.product-legal-entity-scopes.read", "mdm.product-legal-entity-scopes.configure",
        "mdm.product-legal-entity-scopes.replace", "mdm.product-legal-entity-scopes.end",
        "mdm.product-legal-entity-scope-rollout.activate", "mdm.product-legal-entity-scope-rollout.rollback"
    ];

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
}
