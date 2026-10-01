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
    public void Exact_canonical_22_pair_configuration_accepts_every_pair_and_no_other_pair()
    {
        var authenticator = Create();

        Assert.Equal(22, CanonicalPermissions.Length);
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
        options.Mdm.AllowedPairs = Enumerable.Range(0, TrustedLegalEntityScopeCredentialAuthenticator.MaxAllowedPairs + 1)
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

    [Theory]
    [InlineData("mdm.gskus.update")]
    [InlineData("mdm.gskus.submit")]
    [InlineData("mdm.gskus.withdraw")]
    [InlineData("mdm.gskus.request-correction")]
    [InlineData("mdm.gskus.request-retirement")]
    [InlineData("mdm.gskus.retire")]
    public void Gsku_pair_requires_exact_module_permission_and_explicit_configuration(string permission)
    {
        var options = Options();
        options.Mdm.AllowedPairs = [new() { ModuleCode = "product-item-sku-master", PermissionKey = permission }];
        var authenticator = Create(options);
        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.True(authenticator.AllowsPair("product-item-sku-master", permission));
        Assert.False(authenticator.AllowsPair("gskus", permission));
        Assert.False(authenticator.AllowsPair("other-module", permission));
        Assert.False(authenticator.AllowsPair("Product-Item-Sku-Master", permission));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", permission.ToUpperInvariant()));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.gskus.unknown"));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.gskus.*"));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.gskus.read"));
        Assert.False(Create().AllowsPair("product-item-sku-master", permission));
        options.Mdm.AllowedPairs[0].ModuleCode = "gskus";
        Assert.True(Create(options).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
    }

    [Fact]
    public void Exact_28_pair_union_preserves_original_22_and_rejects_other_product_lifecycle()
    {
        var options = Options();
        foreach (var permission in new[] { "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
                     "mdm.gskus.request-correction", "mdm.gskus.request-retirement", "mdm.gskus.retire" })
            options.Mdm.AllowedPairs.Add(new() { ModuleCode = "product-item-sku-master", PermissionKey = permission });
        Assert.Equal(28, options.Mdm.AllowedPairs.Select(p => (p.ModuleCode, p.PermissionKey)).Distinct().Count());
        Assert.Equal(37, TrustedLegalEntityScopeCredentialAuthenticator.MaxAllowedPairs);
        var authenticator = Create(options);
        Assert.All(options.Mdm.AllowedPairs, p => Assert.True(authenticator.AllowsPair(p.ModuleCode, p.PermissionKey)));
        foreach (var permission in new[] { "mdm.global-products.submit", "mdm.lskus.retire", "mdm.finished-goods.submit" })
            Assert.False(authenticator.AllowsPair("product-item-sku-master", permission));
        Assert.False(authenticator.Authenticate("another-client", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
    }

    [Theory]
    [InlineData("mdm.lskus.withdraw")]
    [InlineData("mdm.lskus.request-retirement")]
    [InlineData("mdm.lskus.submit")]
    [InlineData("mdm.lskus.retire")]
    public void Lsku_action_pair_requires_exact_module_permission_and_configured_subset(string permission)
    {
        var options = Options();
        options.Mdm.AllowedPairs = [new() { ModuleCode = "product-item-sku-master", PermissionKey = permission }];
        var authenticator = Create(options);
        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.True(authenticator.AllowsPair("product-item-sku-master", permission));
        Assert.False(Create().AllowsPair("product-item-sku-master", permission));
        foreach (var module in new[] { "lskus", "other-module", "Product-Item-Sku-Master" })
            Assert.False(authenticator.AllowsPair(module, permission));
        foreach (var other in new[] { permission.ToUpperInvariant(), permission + ".extra",
                     "mdm.lskus.*", "mdm.lskus.unknown", "mdm.lskus.read", "mdm.gskus.withdraw" })
            Assert.False(authenticator.AllowsPair("product-item-sku-master", other));
        Assert.False(authenticator.Authenticate("another-client", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        foreach (var invalid in new[] { permission.ToUpperInvariant(), permission + ".extra", "mdm.lskus.*", "mdm.lskus.unknown" })
        {
            options.Mdm.AllowedPairs[0].PermissionKey = invalid;
            Assert.True(Create(options).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
        }
        options.Mdm.AllowedPairs[0].PermissionKey = permission;
        options.Mdm.AllowedPairs[0].ModuleCode = "lskus";
        Assert.True(Create(options).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
    }

    [Fact]
    public void Exact_30_pair_union_preserves_28_existing_pairs_and_adds_only_two_lsku_actions()
    {
        var options = Options();
        foreach (var permission in new[] { "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
                     "mdm.gskus.request-correction", "mdm.gskus.request-retirement", "mdm.gskus.retire",
                     "mdm.lskus.withdraw", "mdm.lskus.request-retirement" })
            options.Mdm.AllowedPairs.Add(new() { ModuleCode = "product-item-sku-master", PermissionKey = permission });
        Assert.Equal(30, options.Mdm.AllowedPairs.Select(p => (p.ModuleCode, p.PermissionKey)).Distinct().Count());
        var authenticator = Create(options);
        Assert.All(options.Mdm.AllowedPairs, p => Assert.True(authenticator.AllowsPair(p.ModuleCode, p.PermissionKey)));
        foreach (var permission in new[] { "mdm.lskus.update", "mdm.lskus.request-correction", "mdm.lskus.submit", "mdm.lskus.retire" })
            Assert.False(authenticator.AllowsPair("product-item-sku-master", permission));
    }

    [Fact]
    public void Exact_32_pair_union_preserves_prior_pairs_without_finished_good_lifecycle_actions()
    {
        var options = Options();
        foreach (var permission in new[] { "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
                     "mdm.gskus.request-correction", "mdm.gskus.request-retirement", "mdm.gskus.retire",
                     "mdm.lskus.withdraw", "mdm.lskus.request-retirement", "mdm.lskus.submit", "mdm.lskus.retire" })
            options.Mdm.AllowedPairs.Add(new() { ModuleCode = "product-item-sku-master", PermissionKey = permission });
        Assert.Equal(32, options.Mdm.AllowedPairs.Select(p => (p.ModuleCode, p.PermissionKey)).Distinct().Count());
        var authenticator = Create(options);
        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.All(options.Mdm.AllowedPairs, p => Assert.True(authenticator.AllowsPair(p.ModuleCode, p.PermissionKey)));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.finished-goods.submit"));
        options.Mdm.AllowedPairs.Add(new() { ModuleCode = "product-item-sku-master", PermissionKey = "mdm.lskus.update" });
        Assert.True(Create(options).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
    }

    [Theory]
    [InlineData("mdm.finished-goods.submit")]
    [InlineData("mdm.finished-goods.cancel-draft")]
    [InlineData("mdm.finished-goods.withdraw")]
    [InlineData("mdm.finished-goods.request-retirement")]
    [InlineData("mdm.finished-goods.withdraw-retirement-request")]
    public void Finished_good_lifecycle_pair_requires_exact_module_permission_and_configured_subset(string permission)
    {
        var options = Options();
        options.Mdm.AllowedPairs = [new() { ModuleCode = "product-item-sku-master", PermissionKey = permission }];
        var authenticator = Create(options);

        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.True(authenticator.AllowsPair("product-item-sku-master", permission));
        Assert.False(authenticator.AllowsPair("finished-goods", permission));
        Assert.False(authenticator.AllowsPair("Product-Item-Sku-Master", permission));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", permission.ToUpperInvariant()));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.finished-goods.*"));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.finished-goods.retire"));
        Assert.False(authenticator.AllowsPair("product-item-sku-master", "mdm.finished-goods.read"));
        Assert.False(Create().AllowsPair("product-item-sku-master", permission));

        options.Mdm.AllowedPairs[0].PermissionKey = "mdm.finished-goods.retire";
        Assert.True(Create(options).Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Forbidden);
    }

    [Fact]
    public void Exact_37_pair_union_preserves_existing_32_and_adds_only_finished_good_lifecycle_actions()
    {
        var options = Options();
        foreach (var permission in new[] { "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
                     "mdm.gskus.request-correction", "mdm.gskus.request-retirement", "mdm.gskus.retire",
                     "mdm.lskus.withdraw", "mdm.lskus.request-retirement", "mdm.lskus.submit", "mdm.lskus.retire",
                     "mdm.finished-goods.submit", "mdm.finished-goods.cancel-draft", "mdm.finished-goods.withdraw",
                     "mdm.finished-goods.request-retirement", "mdm.finished-goods.withdraw-retirement-request" })
            options.Mdm.AllowedPairs.Add(new() { ModuleCode = "product-item-sku-master", PermissionKey = permission });

        Assert.Equal(37, options.Mdm.AllowedPairs.Select(p => (p.ModuleCode, p.PermissionKey)).Distinct().Count());
        Assert.Equal(37, TrustedLegalEntityScopeCredentialAuthenticator.MaxAllowedPairs);
        var authenticator = Create(options);
        Assert.True(authenticator.Authenticate("mdm", "active-secret", TrustedLegalEntityScopeCredentialAuthenticator.Audience).Authenticated);
        Assert.All(options.Mdm.AllowedPairs, p => Assert.True(authenticator.AllowsPair(p.ModuleCode, p.PermissionKey)));
        foreach (var rejected in new[] { "mdm.finished-goods.retire", "mdm.finished-goods.*", "mdm.finished-goods.update" })
            Assert.False(authenticator.AllowsPair("product-item-sku-master", rejected));
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
