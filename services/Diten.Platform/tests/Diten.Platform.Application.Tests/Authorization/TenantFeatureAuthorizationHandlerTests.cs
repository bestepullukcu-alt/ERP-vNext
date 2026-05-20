using System.Security.Claims;
using Diten.Platform.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class TenantFeatureAuthorizationHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task HandleAsync_does_not_succeed_when_user_is_not_authenticated()
    {
        var checker = new Mock<IEntitlementChecker>();
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await CreateHandler(checker).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        checker.Verify(
            x => x.IsFeatureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_succeeds_for_platform_admin_without_checker_call()
    {
        var checker = new Mock<IEntitlementChecker>();
        var auditSink = new Mock<IEntitlementAuditSink>();
        var context = CreateContext(CreatePrincipal(("actor_type", "platform_admin")));

        await CreateHandler(checker, auditSink).HandleAsync(context);

        Assert.True(context.HasSucceeded);
        checker.Verify(
            x => x.IsFeatureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyAuditNotCalled(auditSink);
    }

    [Fact]
    public async Task HandleAsync_does_not_succeed_when_actor_type_is_missing()
    {
        var checker = new Mock<IEntitlementChecker>();
        var context = CreateContext(CreatePrincipal());

        await CreateHandler(checker).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        checker.Verify(
            x => x.IsFeatureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_does_not_succeed_when_tenant_user_has_no_tenant_id()
    {
        var checker = new Mock<IEntitlementChecker>();
        var context = CreateContext(CreatePrincipal(("actor_type", "tenant_user")));

        await CreateHandler(checker).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        checker.Verify(
            x => x.IsFeatureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_does_not_succeed_when_tenant_id_is_invalid()
    {
        var checker = new Mock<IEntitlementChecker>();
        var context = CreateContext(CreatePrincipal(("actor_type", "tenant_user"), ("tenant_id", "not-a-guid")));

        await CreateHandler(checker).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        checker.Verify(
            x => x.IsFeatureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_succeeds_when_tenant_user_has_allowed_feature()
    {
        var checker = new Mock<IEntitlementChecker>();
        checker
            .Setup(x => x.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntitlementCheckResult.Allowed(EntitlementKind.Feature, "ADVANCED_REPORTING"));
        var auditSink = new Mock<IEntitlementAuditSink>();
        var context = CreateContext(CreatePrincipal(("actor_type", "tenant_user"), ("tenant_id", TenantId.ToString())));

        await CreateHandler(checker, auditSink).HandleAsync(context);

        Assert.True(context.HasSucceeded);
        VerifyAuditNotCalled(auditSink);
    }

    [Fact]
    public async Task HandleAsync_does_not_succeed_when_feature_is_denied()
    {
        var checker = new Mock<IEntitlementChecker>();
        var auditSink = new Mock<IEntitlementAuditSink>();
        checker
            .Setup(x => x.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntitlementCheckResult.Denied(
                EntitlementKind.Feature,
                "ADVANCED_REPORTING",
                EntitlementDenyReason.FeatureNotEnabled));
        var context = CreateContext(CreatePrincipal(("actor_type", "tenant_user"), ("tenant_id", TenantId.ToString())));

        await CreateHandler(checker, auditSink).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
        Assert.Contains(
            context.FailureReasons,
            reason => reason.Message.Contains(nameof(EntitlementDenyReason.FeatureNotEnabled), StringComparison.Ordinal));
        auditSink.Verify(
            x => x.LogDeniedAsync(
                It.Is<EntitlementAuditDenyContext>(audit =>
                    audit.TenantId == TenantId
                    && audit.ActorType == "tenant_user"
                    && audit.EntitlementKind == EntitlementKind.Feature
                    && audit.Code == "ADVANCED_REPORTING"
                    && audit.DenyReason == EntitlementDenyReason.FeatureNotEnabled
                    && !audit.IsTransientFailure
                    && audit.Source == nameof(TenantFeatureAuthorizationHandler)
                    && audit.RequirementName == nameof(TenantFeatureRequirement)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_remains_denied_when_feature_audit_sink_throws()
    {
        var checker = new Mock<IEntitlementChecker>();
        var auditSink = new Mock<IEntitlementAuditSink>();
        checker
            .Setup(x => x.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntitlementCheckResult.Denied(
                EntitlementKind.Feature,
                "ADVANCED_REPORTING",
                EntitlementDenyReason.FeatureNotEnabled,
                isCacheable: false));
        auditSink
            .Setup(x => x.LogDeniedAsync(It.IsAny<EntitlementAuditDenyContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var context = CreateContext(CreatePrincipal(("actor_type", "tenant_user"), ("tenant_id", TenantId.ToString())));

        await CreateHandler(checker, auditSink).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
        auditSink.Verify(
            x => x.LogDeniedAsync(
                It.Is<EntitlementAuditDenyContext>(audit => audit.IsTransientFailure),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_fails_closed_when_checker_throws()
    {
        var checker = new Mock<IEntitlementChecker>();
        checker
            .Setup(x => x.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var context = CreateContext(CreatePrincipal(("actor_type", "tenant_user"), ("tenant_id", TenantId.ToString())));

        await CreateHandler(checker).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task HandleAsync_fails_closed_for_partner_admin()
    {
        var checker = new Mock<IEntitlementChecker>();
        var context = CreateContext(CreatePrincipal(("actor_type", "partner_admin"), ("tenant_id", TenantId.ToString())));

        await CreateHandler(checker).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        checker.Verify(
            x => x.IsFeatureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user)
    {
        return new AuthorizationHandlerContext([new TenantFeatureRequirement("ADVANCED_REPORTING")], user, null);
    }

    private static TenantFeatureAuthorizationHandler CreateHandler(
        Mock<IEntitlementChecker> checker,
        Mock<IEntitlementAuditSink>? auditSink = null)
    {
        auditSink ??= new Mock<IEntitlementAuditSink>();
        return new TenantFeatureAuthorizationHandler(checker.Object, auditSink.Object);
    }

    private static void VerifyAuditNotCalled(Mock<IEntitlementAuditSink> auditSink)
    {
        auditSink.Verify(
            x => x.LogDeniedAsync(It.IsAny<EntitlementAuditDenyContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            "Test"));
    }
}
