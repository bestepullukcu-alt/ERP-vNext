using System.Security.Claims;
using Diten.Platform.API.Security;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

// AG-STEP-011 / MOD-0018-FU14 Group A — the shared pure permission-claim evaluator. These narrow tests pin the
// provenance classification (canonical vs GENUINE legacy alias vs missing) and confirm the evaluator is side-effect-free.
// Key rule: a case-variant of the canonical key (e.g. PLATFORM.AUDIT.READ) is CANONICAL provenance, not a legacy alias;
// only a distinct mapped key string (e.g. Modules.OrganizationUnit.Read) is a LegacyAliasMatch. The byte-for-byte
// enforcement equivalence is held separately by HasPermissionAttributeDualReadTests.
public sealed class PermissionClaimEvaluatorTests
{
    private const string Canonical = "platform.administrators.read";

    // A genuine legacy alias: a distinct key string (not a case-variant of the canonical) that maps to it.
    private const string AliasCanonical = "platform.organization-units.read";
    private const string GenuineAlias = "Modules.OrganizationUnit.Read";

    private static IEnumerable<Claim> Permissions(params string[] values)
        => values.Select(v => new Claim("permission", v));

    [Fact]
    public void Exact_lowercase_canonical_claim_is_canonical_match()
    {
        var result = PermissionClaimEvaluator.Evaluate(Permissions(Canonical), Canonical);

        Assert.True(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.CanonicalMatch, result.Match);
        Assert.False(result.MatchedViaLegacyAlias);
        Assert.Equal(Canonical, result.RequiredPermission);
    }

    [Fact]
    public void Mixed_case_canonical_claim_is_canonical_match_not_legacy_alias()
    {
        // A case-variant of the canonical key is canonical provenance, NOT a legacy alias.
        var result = PermissionClaimEvaluator.Evaluate(Permissions("PLATFORM.AUDIT.READ"), "platform.audit.read");

        Assert.True(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.CanonicalMatch, result.Match);
        Assert.False(result.MatchedViaLegacyAlias);
    }

    [Fact]
    public void Genuine_legacy_alias_claim_is_legacy_alias_match()
    {
        var result = PermissionClaimEvaluator.Evaluate(Permissions(GenuineAlias), AliasCanonical);

        Assert.True(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.LegacyAliasMatch, result.Match);
        Assert.True(result.MatchedViaLegacyAlias);
    }

    [Fact]
    public void Genuine_legacy_alias_with_different_casing_is_legacy_alias_match()
    {
        // A case-variation of a genuine alias still resolves to that alias (case-insensitive) → legacy-alias provenance.
        var result = PermissionClaimEvaluator.Evaluate(Permissions("modules.organizationunit.read"), AliasCanonical);

        Assert.True(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.LegacyAliasMatch, result.Match);
        Assert.True(result.MatchedViaLegacyAlias);
    }

    [Fact]
    public void Canonical_is_preferred_when_both_canonical_and_genuine_alias_present()
    {
        // Alias listed first; canonical classification still wins and the alias flag is false.
        var result = PermissionClaimEvaluator.Evaluate(
            Permissions(GenuineAlias, AliasCanonical),
            AliasCanonical);

        Assert.Equal(PermissionClaimMatch.CanonicalMatch, result.Match);
        Assert.False(result.MatchedViaLegacyAlias);
    }

    [Fact]
    public void Missing_permission_claim_is_not_satisfied()
    {
        var result = PermissionClaimEvaluator.Evaluate(new[] { new Claim("actor_type", "tenant_user") }, Canonical);

        Assert.False(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.MissingClaim, result.Match);
        Assert.False(result.MatchedViaLegacyAlias);
    }

    [Fact]
    public void Unrelated_permission_claim_is_not_satisfied()
    {
        var result = PermissionClaimEvaluator.Evaluate(Permissions("some.other.permission"), Canonical);

        Assert.False(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.MissingClaim, result.Match);
    }

    [Fact]
    public void Space_separated_tokens_in_a_single_claim_are_matched()
    {
        var result = PermissionClaimEvaluator.Evaluate(
            new[] { new Claim("permission", $"a.b.c {Canonical}") },
            Canonical);

        Assert.True(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.CanonicalMatch, result.Match);
    }

    [Fact]
    public void Permissions_plural_claim_type_is_recognized()
    {
        var result = PermissionClaimEvaluator.Evaluate(new[] { new Claim("permissions", Canonical) }, Canonical);

        Assert.True(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.CanonicalMatch, result.Match);
    }

    [Fact]
    public void Legacy_requirement_is_satisfied_only_by_its_own_form()
    {
        // The required key is itself a legacy alias; the resolver does not expand it (no auto-upgrade). A claim of the
        // same key (case-insensitive) is canonical provenance for that requirement; an unrelated claim is missing.
        var ownForm = PermissionClaimEvaluator.Evaluate(
            Permissions("Platform.Administrators.Read"),
            "Platform.Administrators.Read");
        Assert.True(ownForm.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.CanonicalMatch, ownForm.Match);
        Assert.False(ownForm.MatchedViaLegacyAlias);

        var unrelated = PermissionClaimEvaluator.Evaluate(
            Permissions("platform.subscription-plans.read"),
            "Platform.Administrators.Read");
        Assert.False(unrelated.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.MissingClaim, unrelated.Match);
    }

    [Fact]
    public void Result_carries_only_the_required_key_and_is_deterministic()
    {
        // The bounded result echoes the required key and exposes no raw claim/alias value; repeated calls are stable
        // (pure function — no repository / HttpContext / external-service access).
        var first = PermissionClaimEvaluator.Evaluate(Permissions(GenuineAlias), AliasCanonical);
        var second = PermissionClaimEvaluator.Evaluate(Permissions(GenuineAlias), AliasCanonical);

        Assert.Equal(AliasCanonical, first.RequiredPermission);
        Assert.Equal(first, second);
    }

    [Fact]
    public void No_permission_claims_at_all_is_missing()
    {
        var result = PermissionClaimEvaluator.Evaluate(Array.Empty<Claim>(), Canonical);

        Assert.False(result.IsSatisfied);
        Assert.Equal(PermissionClaimMatch.MissingClaim, result.Match);
    }
}
