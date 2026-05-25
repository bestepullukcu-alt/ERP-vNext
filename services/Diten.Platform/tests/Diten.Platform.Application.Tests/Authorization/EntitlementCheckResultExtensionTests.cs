using Diten.Platform.Common.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class EntitlementCheckResultExtensionTests
{
    [Fact]
    public void Allowed_three_arg_overload_returns_default_metadata()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var result = EntitlementCheckResult.Allowed(EntitlementKind.Module, "HR");

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.True(result.IsAllowed);
        Assert.Equal(EntitlementKind.Module, result.Kind);
        Assert.Equal("HR", result.Code);
        Assert.Null(result.DenyReason);
        Assert.Null(result.ExpiresAtUtc);
        Assert.True(result.IsCacheable);
        Assert.NotNull(result.EffectiveScopes);
        Assert.Empty(result.EffectiveScopes);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
        Assert.InRange(result.ResolvedAtUtc, before, after);
    }

    [Fact]
    public void Denied_overload_returns_default_metadata_and_preserves_reason()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var result = EntitlementCheckResult.Denied(
            EntitlementKind.Feature,
            "FEATURE_X",
            EntitlementDenyReason.FeatureNotEnabled);

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.False(result.IsAllowed);
        Assert.Equal(EntitlementDenyReason.FeatureNotEnabled, result.DenyReason);
        Assert.NotNull(result.EffectiveScopes);
        Assert.Empty(result.EffectiveScopes);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
        Assert.InRange(result.ResolvedAtUtc, before, after);
    }

    [Fact]
    public void Allowed_six_arg_overload_propagates_metadata()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var resolvedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var scopes = new[]
        {
            new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, Guid.NewGuid(), "ORG-A"),
            new EntitlementDataScope(EntitlementDataScopeKind.Country, "TR")
        };

        var result = EntitlementCheckResult.Allowed(
            EntitlementKind.Module,
            "HR",
            expires,
            scopes,
            EntitlementResolutionSource.Override,
            resolvedAt);

        Assert.True(result.IsAllowed);
        Assert.Equal(expires, result.ExpiresAtUtc);
        Assert.Equal(2, result.EffectiveScopes.Count);
        Assert.Equal(EntitlementResolutionSource.Override, result.ResolvedFrom);
        Assert.Equal(resolvedAt, result.ResolvedAtUtc);
    }

    [Fact]
    public void Allowed_six_arg_overload_normalizes_null_scopes_to_empty()
    {
        var result = EntitlementCheckResult.Allowed(
            EntitlementKind.Module,
            "HR",
            expiresAtUtc: null,
            effectiveScopes: null,
            resolvedFrom: EntitlementResolutionSource.Plan);

        Assert.NotNull(result.EffectiveScopes);
        Assert.Empty(result.EffectiveScopes);
    }

    [Fact]
    public void EffectiveScopes_init_null_normalizes_to_empty_on_read()
    {
        var result = new EntitlementCheckResult(true, EntitlementKind.Module, "HR")
        {
            EffectiveScopes = null!
        };

        Assert.NotNull(result.EffectiveScopes);
        Assert.Empty(result.EffectiveScopes);
    }

    [Fact]
    public void Positional_constructor_preserves_legacy_call_site_compilation()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(1);

        var result = new EntitlementCheckResult(
            true,
            EntitlementKind.Module,
            "HR",
            null,
            expires,
            true);

        Assert.True(result.IsAllowed);
        Assert.Equal(EntitlementKind.Module, result.Kind);
        Assert.Equal("HR", result.Code);
        Assert.Null(result.DenyReason);
        Assert.Equal(expires, result.ExpiresAtUtc);
        Assert.True(result.IsCacheable);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
        Assert.NotNull(result.EffectiveScopes);
        Assert.Empty(result.EffectiveScopes);
    }

    [Fact]
    public void With_expression_preserves_original_resolved_at_utc()
    {
        var original = EntitlementCheckResult.Allowed(EntitlementKind.Module, "HR");
        var originalResolvedAt = original.ResolvedAtUtc;

        var modified = original with { Code = "FIN" };

        Assert.Equal(originalResolvedAt, modified.ResolvedAtUtc);
        Assert.Equal("FIN", modified.Code);
    }

    [Fact]
    public void Records_with_default_metadata_are_value_equal()
    {
        var fixedNow = DateTimeOffset.UtcNow;
        var a = new EntitlementCheckResult(true, EntitlementKind.Module, "HR")
        {
            ResolvedAtUtc = fixedNow
        };
        var b = new EntitlementCheckResult(true, EntitlementKind.Module, "HR")
        {
            ResolvedAtUtc = fixedNow
        };

        Assert.Equal(a, b);
    }
}
