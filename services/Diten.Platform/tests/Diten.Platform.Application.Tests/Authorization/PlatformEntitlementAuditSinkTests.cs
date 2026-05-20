using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class PlatformEntitlementAuditSinkTests
{
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task LogDeniedAsync_maps_module_deny_to_audit_append_request()
    {
        var auditService = new Mock<IAuditService>();
        AuditAppendRequest? captured = null;
        auditService
            .Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AuditAppendRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(AuditAppendResult.Queued("audit-1"));
        var sink = CreateSink(auditService);

        await sink.LogDeniedAsync(
            new EntitlementAuditDenyContext(
                TenantId,
                "tenant_user",
                EntitlementKind.Module,
                "HR",
                EntitlementDenyReason.ModuleNotEntitled,
                IsTransientFailure: false,
                Source: nameof(TenantModuleAuthorizationHandler),
                RequirementName: nameof(TenantModuleRequirement)),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(AuditCategory.Security, captured.Category);
        Assert.Equal(AuditOperation.PermissionDenied, captured.Operation);
        Assert.Equal(AuditOutcome.Denied, captured.Outcome);
        Assert.Equal(AuditActorType.TenantUser, captured.ActorType);
        Assert.Equal("ModuleAccess", captured.EntityType);
        Assert.Equal(TenantId, captured.TargetTenantId);
        Assert.Equal("Diten.Platform", captured.SourceService);
        Assert.Equal("MOD-0018", captured.SourceModule);
        Assert.Equal("Module", captured.Metadata["entitlementKind"]);
        Assert.Equal("HR", captured.Metadata["code"]);
        Assert.Equal("ModuleNotEntitled", captured.Metadata["denyReason"]);
        Assert.Equal(false, captured.Metadata["isTransientFailure"]);
        Assert.Equal(nameof(TenantModuleAuthorizationHandler), captured.Metadata["source"]);
        Assert.Equal(nameof(TenantModuleRequirement), captured.Metadata["requirementName"]);
    }

    [Fact]
    public async Task LogDeniedAsync_maps_feature_deny_to_audit_append_request()
    {
        var auditService = new Mock<IAuditService>();
        AuditAppendRequest? captured = null;
        auditService
            .Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AuditAppendRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(AuditAppendResult.Queued("audit-2"));
        var sink = CreateSink(auditService);

        await sink.LogDeniedAsync(
            new EntitlementAuditDenyContext(
                TenantId,
                "tenant_user",
                EntitlementKind.Feature,
                "ADVANCED_REPORTING",
                EntitlementDenyReason.FeatureNotEnabled,
                IsTransientFailure: true,
                Source: nameof(TenantFeatureAuthorizationHandler),
                RequirementName: nameof(TenantFeatureRequirement)),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(AuditCategory.Security, captured.Category);
        Assert.Equal(AuditOperation.PermissionDenied, captured.Operation);
        Assert.Equal(AuditOutcome.Denied, captured.Outcome);
        Assert.Equal(AuditActorType.TenantUser, captured.ActorType);
        Assert.Equal("FeatureAccess", captured.EntityType);
        Assert.Equal(TenantId, captured.TargetTenantId);
        Assert.Equal("Feature", captured.Metadata["entitlementKind"]);
        Assert.Equal("ADVANCED_REPORTING", captured.Metadata["code"]);
        Assert.Equal("FeatureNotEnabled", captured.Metadata["denyReason"]);
        Assert.Equal(true, captured.Metadata["isTransientFailure"]);
    }

    [Fact]
    public async Task LogDeniedAsync_does_not_throw_when_audit_service_throws()
    {
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var sink = CreateSink(auditService);

        var exception = await Record.ExceptionAsync(() => sink.LogDeniedAsync(
            new EntitlementAuditDenyContext(
                TenantId,
                "tenant_user",
                EntitlementKind.Module,
                "HR",
                EntitlementDenyReason.ModuleNotEntitled,
                IsTransientFailure: false,
                Source: nameof(TenantModuleAuthorizationHandler),
                RequirementName: nameof(TenantModuleRequirement)),
            CancellationToken.None));

        Assert.Null(exception);
        auditService.Verify(
            x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static PlatformEntitlementAuditSink CreateSink(Mock<IAuditService> auditService)
    {
        return new PlatformEntitlementAuditSink(
            auditService.Object,
            NullLogger<PlatformEntitlementAuditSink>.Instance);
    }
}
