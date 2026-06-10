using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Authorization.Explain;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

// AG-STEP-011 / MOD-0018-FU14 Group B/C — self-explain observer service. Bounded, self-subject-only, read-only.
// Group C correctness: the response returns TWO SEPARATE observations (PermissionSatisfied + descriptive scope) and NO
// combined Allowed verdict — data-scope is opt-in per resource and platform/partner admins are not row-scoped, so an
// empty scope must NOT flip the permission observation (that would diverge from live enforcement).
public sealed class SelfAccessExplainServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string Canonical = "platform.administrators.read";
    private const string Module = "MOD-0018";
    private const string EmptyScopeNote = "empty-scope-does-not-imply-universal-deny";
    private const string ScopeFailedNote = "scope-observation-failed";

    private readonly Mock<ITenantAuthorizationContext> _authContext = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IDataScopeResolver> _resolver = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<ICorrelationContext> _correlation = new();
    private AuditAppendRequest? _capturedAudit;

    public SelfAccessExplainServiceTests()
    {
        _authContext.SetupGet(c => c.TenantId).Returns(TenantId);
        _authContext.SetupGet(c => c.UserId).Returns(UserId);
        _authContext.SetupGet(c => c.ActorType).Returns("tenant_user");
        _authContext.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _currentUser.SetupGet(c => c.Email).Returns("user@example.com");
        _currentUser.SetupGet(c => c.DisplayName).Returns("Test User");
        _correlation.SetupGet(c => c.CorrelationId).Returns(Guid.NewGuid().ToString("N"));
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<EntitlementDataScope>)Array.Empty<EntitlementDataScope>());
        _audit
            .Setup(a => a.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AuditAppendRequest, CancellationToken>((req, _) => _capturedAudit = req)
            .ReturnsAsync(AuditAppendResult.Queued("idem"));
    }

    private SelfAccessExplainService CreateService() => new(
        _authContext.Object, _currentUser.Object, _resolver.Object, _audit.Object, _correlation.Object,
        NullLogger<SelfAccessExplainService>.Instance);

    private static ClaimsPrincipal Principal(string[]? permissions = null, long? expUnixSeconds = null)
    {
        var claims = new List<Claim>();
        if (permissions is not null)
        {
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        }
        if (expUnixSeconds is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Exp, expUnixSeconds.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private void WithScopes(params EntitlementDataScope[] scopes) =>
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<EntitlementDataScope>)scopes);

    private static EntitlementDataScope Scope(EntitlementDataScopeKind kind) =>
        new(kind, scopeId: Guid.NewGuid(), scopeCode: "code");

    // ── DTO contract: separate observations, no combined verdict ──

    [Fact]
    public void Response_dto_has_separate_observations_and_no_combined_verdict()
    {
        var props = typeof(SelfAccessExplainResponse).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains("PermissionSatisfied", props);
        Assert.Contains("ScopeNotes", props);
        Assert.DoesNotContain("Allowed", props);
        Assert.DoesNotContain("ScopeApplicable", props);
        Assert.DoesNotContain("ScopeDenied", props);
    }

    // ── Request / validation (validation deny reaches the service → audited, bounded) ──

    [Fact]
    public async Task Blank_permission_key_is_rejected_400_and_audited()
    {
        var result = await CreateService().ExplainAsync(Principal(), " ", Module, null, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(_capturedAudit);
        Assert.Equal(AuditOutcome.Denied, _capturedAudit!.Outcome);
        Assert.Equal("permission-key-required", _capturedAudit.Metadata["validationCode"]);
        Assert.Equal(false, _capturedAudit.Metadata["permissionSatisfied"]);
        // The raw invalid input is never written to metadata.
        Assert.False(_capturedAudit.Metadata.ContainsKey("requiredPermission"));
    }

    [Fact]
    public async Task Blank_module_code_is_rejected_400_and_audited()
    {
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, "  ", null, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(AuditOutcome.Denied, _capturedAudit!.Outcome);
        Assert.Equal("module-code-required", _capturedAudit.Metadata["validationCode"]);
    }

    [Fact]
    public async Task Invalid_authenticated_context_fails_closed_403_and_is_audited()
    {
        _authContext.SetupGet(c => c.UserId).Returns(Guid.Empty);

        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(AuditOutcome.Denied, _capturedAudit!.Outcome);
        Assert.Equal("authenticated-context-invalid", _capturedAudit.Metadata["validationCode"]);
        Assert.False(_capturedAudit.Metadata.ContainsKey("exception"));
    }

    [Fact]
    public async Task Validation_deny_response_survives_an_audit_exception()
    {
        _audit
            .Setup(a => a.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit down"));

        var result = await CreateService().ExplainAsync(Principal(), " ", Module, null, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Validation_deny_propagates_audit_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _audit
            .Setup(a => a.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateService().ExplainAsync(Principal(), " ", Module, null, cts.Token));
    }

    [Fact]
    public async Task Blank_feature_code_is_normalized_to_null_for_the_resolver()
    {
        string? seenFeature = "unset";
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string?, CancellationToken>((_, _, _, f, _) => seenFeature = f)
            .ReturnsAsync((IReadOnlyList<EntitlementDataScope>)Array.Empty<EntitlementDataScope>());

        await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, "   ", CancellationToken.None);

        Assert.Null(seenFeature);
    }

    [Fact]
    public async Task Resolver_receives_context_tenant_and_user_not_the_request()
    {
        await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        _resolver.Verify(r => r.ResolveAsync(TenantId, UserId, Module, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Permission observation (independent of data-scope) ──

    [Fact]
    public async Task Canonical_claim_is_permission_satisfied()
    {
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.Equal("canonical", result.Data!.PermissionMatch);
        Assert.False(result.Data.MatchedViaLegacyAlias);
        Assert.True(result.Data.PermissionSatisfied);
    }

    [Fact]
    public async Task Mixed_case_canonical_claim_is_canonical_match()
    {
        var result = await CreateService().ExplainAsync(Principal(["PLATFORM.ADMINISTRATORS.READ"]), Canonical, Module, null, CancellationToken.None);
        Assert.Equal("canonical", result.Data!.PermissionMatch);
        Assert.False(result.Data.MatchedViaLegacyAlias);
        Assert.True(result.Data.PermissionSatisfied);
    }

    [Fact]
    public async Task Genuine_legacy_alias_claim_is_legacy_alias_match_and_satisfied()
    {
        var result = await CreateService().ExplainAsync(
            Principal(["Modules.OrganizationUnit.Read"]), "platform.organization-units.read", Module, null, CancellationToken.None);
        Assert.Equal("legacy-alias", result.Data!.PermissionMatch);
        Assert.True(result.Data.MatchedViaLegacyAlias);
        Assert.True(result.Data.PermissionSatisfied);
    }

    [Fact]
    public async Task Missing_claim_is_not_permission_satisfied()
    {
        var result = await CreateService().ExplainAsync(Principal(), Canonical, Module, null, CancellationToken.None);
        Assert.Equal("missing", result.Data!.PermissionMatch);
        Assert.False(result.Data.PermissionSatisfied);
    }

    // ── Parity with live enforcement: empty scope must NOT flip PermissionSatisfied ──

    [Fact]
    public async Task Canonical_claim_with_empty_scope_is_still_permission_satisfied()
    {
        WithScopes(); // empty — but permission is satisfied; empty scope is descriptive, not a universal deny
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.True(result.Data!.PermissionSatisfied);
        Assert.Empty(result.Data.ScopeKinds);
        Assert.Contains(EmptyScopeNote, result.Data.ScopeNotes);
    }

    [Fact]
    public async Task Platform_admin_with_empty_scope_is_satisfied_no_false_negative()
    {
        // Real-world case: platform admins are NOT row-scoped and have empty org scopes. The explain must NOT report a
        // false negative just because the scope is empty.
        _authContext.SetupGet(c => c.ActorType).Returns("platform_admin");
        WithScopes(); // empty
        var result = await CreateService().ExplainAsync(Principal(), Canonical, Module, null, CancellationToken.None);
        Assert.Equal("bypass-platform-admin", result.Data!.PermissionMatch);
        Assert.True(result.Data.PermissionSatisfied);
        Assert.Empty(result.Data.ScopeKinds);
    }

    [Fact]
    public async Task Partner_admin_with_empty_scope_is_satisfied_no_false_negative()
    {
        _authContext.SetupGet(c => c.ActorType).Returns("partner_admin");
        WithScopes(); // empty
        var result = await CreateService().ExplainAsync(Principal(), Canonical, Module, null, CancellationToken.None);
        Assert.Equal("bypass-partner-admin", result.Data!.PermissionMatch);
        Assert.True(result.Data.PermissionSatisfied);
    }

    // ── permissionKey ↔ moduleCode independence (no binding catalog) ──

    [Fact]
    public async Task PermissionKey_and_moduleCode_are_independent_observations()
    {
        // A mismatched (permission, module) pair: the permission observation comes ONLY from the claim, the scope
        // observation ONLY from the module input; no combined verdict and no binding claim is made.
        const string unrelatedModule = "some-unrelated-module";
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, unrelatedModule, null, CancellationToken.None);

        Assert.True(result.Data!.PermissionSatisfied); // from the claim only
        _resolver.Verify(r => r.ResolveAsync(TenantId, UserId, unrelatedModule, null, It.IsAny<CancellationToken>()), Times.Once);
        // Compile-time: there is no Allowed property to combine them (asserted by the DTO contract test).
    }

    // ── Data-scope observation ──

    [Fact]
    public async Task Scope_response_is_kinds_and_counts_only()
    {
        WithScopes(Scope(EntitlementDataScopeKind.OrgUnit), Scope(EntitlementDataScopeKind.OrgUnit), Scope(EntitlementDataScopeKind.Position));
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.Contains("OrgUnit", result.Data!.ScopeKinds);
        Assert.Contains("Position", result.Data.ScopeKinds);
        Assert.Equal(2, result.Data.ScopeCounts["OrgUnit"]);
        Assert.Equal(1, result.Data.ScopeCounts["Position"]);
    }

    [Fact]
    public async Task Scope_notes_carry_the_bounded_opt_in_vocabulary()
    {
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.Contains("data-scope-opt-in-per-resource", result.Data!.ScopeNotes);
        Assert.Contains("scope-applicability-not-determined", result.Data.ScopeNotes);
    }

    [Fact]
    public async Task Resolver_exception_is_a_diagnostic_failure_and_preserves_permission()
    {
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("resolver boom"));

        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.DiagnosticFailure);
        // The permission observation is preserved; the resolver failure does NOT flip it.
        Assert.True(result.Data.PermissionSatisfied);
        Assert.Empty(result.Data.ScopeKinds);
        Assert.Contains(ScopeFailedNote, result.Data.ScopeNotes);
    }

    [Fact]
    public async Task Resolver_cancellation_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, cts.Token));
    }

    // ── Token freshness ──

    [Fact]
    public async Task Expiry_is_parsed_when_present()
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
        var result = await CreateService().ExplainAsync(Principal([Canonical], exp), Canonical, Module, null, CancellationToken.None);
        Assert.NotNull(result.Data!.TokenExpiresAtUtc);
        Assert.Equal(exp, result.Data.TokenExpiresAtUtc!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Expiry_is_null_when_absent()
    {
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.Null(result.Data!.TokenExpiresAtUtc);
    }

    [Fact]
    public async Task Freshness_notes_are_bounded_and_static()
    {
        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.Equal(
            new[] { "refresh-required-after-grant-change", "token-version-unavailable", "revocation-timestamp-unavailable" },
            result.Data!.FreshnessNotes);
        Assert.Equal("self", result.Data.Mode);
    }

    // ── Audit (Outcome from the permission-gate observation, never the removed combined verdict) ──

    [Fact]
    public async Task Satisfied_permission_is_audited_as_succeeded()
    {
        await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.NotNull(_capturedAudit);
        Assert.Equal(AuditOutcome.Succeeded, _capturedAudit!.Outcome);
        Assert.Equal(AuditCategory.Security, _capturedAudit.Category);
        Assert.Equal(TenantId, _capturedAudit.TargetTenantId);
    }

    [Fact]
    public async Task Missing_permission_is_audited_as_denied()
    {
        await CreateService().ExplainAsync(Principal(), Canonical, Module, null, CancellationToken.None);
        Assert.Equal(AuditOutcome.Denied, _capturedAudit!.Outcome);
    }

    [Fact]
    public async Task Diagnostic_failure_is_audited_as_failed()
    {
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        Assert.Equal(AuditOutcome.Failed, _capturedAudit!.Outcome);
        // The permission observation is still recorded in metadata as satisfied.
        Assert.Equal(true, _capturedAudit.Metadata["permissionSatisfied"]);
    }

    [Fact]
    public async Task Audit_exception_does_not_change_the_result()
    {
        _audit
            .Setup(a => a.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit down"));
        WithScopes(Scope(EntitlementDataScopeKind.OrgUnit));

        var result = await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.Data!.PermissionSatisfied);
    }

    [Fact]
    public async Task Audit_metadata_is_bounded_with_permission_satisfied_and_no_allowed()
    {
        await CreateService().ExplainAsync(Principal([Canonical]), Canonical, Module, null, CancellationToken.None);
        var meta = _capturedAudit!.Metadata;
        Assert.Equal("self", meta["mode"]);
        Assert.Equal(Canonical, meta["requiredPermission"]);
        Assert.Contains("permissionSatisfied", meta.Keys);
        Assert.Contains("scopeNotes", meta.Keys);
        Assert.False(meta.ContainsKey("allowed")); // combined verdict removed
        Assert.DoesNotContain("exception", meta.Keys);
        Assert.DoesNotContain("stackTrace", meta.Keys);
        Assert.IsType<string>(meta["scopeKinds"]);
    }
}
