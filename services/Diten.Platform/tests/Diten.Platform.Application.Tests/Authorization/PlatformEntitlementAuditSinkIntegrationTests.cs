using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class PlatformEntitlementAuditSinkIntegrationTests
{
    private static readonly Guid ActiveTenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ActiveUserId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task LogDeniedAsync_Module_Produces_Expected_Outbox_Payload()
    {
        // Arrange
        var outboxWriterMock = new Mock<IAuditOutboxWriter>();
        AuditOutboxWriteRequest? capturedOutboxRequest = null;
        outboxWriterMock
            .Setup(x => x.TryEnqueueAsync(It.IsAny<AuditOutboxWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AuditOutboxWriteRequest, CancellationToken>((req, _) => capturedOutboxRequest = req)
            .ReturnsAsync(true);

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        tenantContextMock.Setup(x => x.TenantId).Returns(ActiveTenantId);

        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.Setup(x => x.UserId).Returns(ActiveUserId);
        userContextMock.Setup(x => x.Email).Returns("test@diten.com");
        userContextMock.Setup(x => x.DisplayName).Returns("Test User");

        var registry = new SensitiveFieldRedactionRegistry();
        var redactor = new SensitiveFieldRedactor(registry);
        var idempotencyKeyBuilder = new AuditIdempotencyKeyBuilder();
        var recursionGuard = new AuditRecursionGuard();

        var auditService = new AuditService(
            outboxWriterMock.Object,
            redactor,
            idempotencyKeyBuilder,
            recursionGuard,
            tenantContextMock.Object,
            userContextMock.Object,
            NullLogger<AuditService>.Instance);

        var sink = new PlatformEntitlementAuditSink(auditService, NullLogger<PlatformEntitlementAuditSink>.Instance);

        var denyContext = new EntitlementAuditDenyContext(
            ActiveTenantId,
            "tenant_user",
            EntitlementKind.Module,
            "HR",
            EntitlementDenyReason.ModuleNotEntitled,
            IsTransientFailure: false,
            Source: "IntegrationTest",
            RequirementName: "ModuleReq");

        // Act
        await sink.LogDeniedAsync(denyContext, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedOutboxRequest);
        Assert.Equal(ActiveTenantId, capturedOutboxRequest.TenantId);
        Assert.Equal(AuditOperation.PermissionDenied, capturedOutboxRequest.Operation);
        Assert.Equal("ModuleAccess", capturedOutboxRequest.EntityType);
        Assert.Equal("EntitlementAccessDenied", capturedOutboxRequest.RequestType);

        var payload = capturedOutboxRequest.Payload;
        Assert.NotNull(payload);
        Assert.Equal(ActiveTenantId, payload["TenantId"]);
        Assert.Equal(ActiveTenantId, payload["TargetTenantId"]);
        Assert.Equal(AuditCategory.Security.ToString(), payload["Category"]);
        Assert.Equal("ModuleAccess", payload["EntityType"]);
        Assert.Equal(AuditOperation.PermissionDenied.ToString(), payload["Operation"]);
        Assert.Equal(AuditOutcome.Denied.ToString(), payload["Outcome"]);
        Assert.Equal(AuditActorType.TenantUser.ToString(), payload["ActorType"]);
        Assert.Equal(ActiveUserId, payload["ActorId"]);
        
        var metadata = payload["Metadata"] as IReadOnlyDictionary<string, object?>;
        Assert.NotNull(metadata);
        Assert.Equal("Module", metadata["entitlementKind"]);
        Assert.Equal("HR", metadata["code"]);
        Assert.Equal("ModuleNotEntitled", metadata["denyReason"]);
        Assert.Equal(ActiveTenantId, metadata["tenantId"]);
    }

    [Fact]
    public async Task LogDeniedAsync_Feature_Produces_Expected_Outbox_Payload()
    {
        // Arrange
        var outboxWriterMock = new Mock<IAuditOutboxWriter>();
        AuditOutboxWriteRequest? capturedOutboxRequest = null;
        outboxWriterMock
            .Setup(x => x.TryEnqueueAsync(It.IsAny<AuditOutboxWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AuditOutboxWriteRequest, CancellationToken>((req, _) => capturedOutboxRequest = req)
            .ReturnsAsync(true);

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        tenantContextMock.Setup(x => x.TenantId).Returns(ActiveTenantId);

        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.Setup(x => x.UserId).Returns(ActiveUserId);
        userContextMock.Setup(x => x.Email).Returns("test@diten.com");
        userContextMock.Setup(x => x.DisplayName).Returns("Test User");

        var registry = new SensitiveFieldRedactionRegistry();
        var redactor = new SensitiveFieldRedactor(registry);
        var idempotencyKeyBuilder = new AuditIdempotencyKeyBuilder();
        var recursionGuard = new AuditRecursionGuard();

        var auditService = new AuditService(
            outboxWriterMock.Object,
            redactor,
            idempotencyKeyBuilder,
            recursionGuard,
            tenantContextMock.Object,
            userContextMock.Object,
            NullLogger<AuditService>.Instance);

        var sink = new PlatformEntitlementAuditSink(auditService, NullLogger<PlatformEntitlementAuditSink>.Instance);

        var denyContext = new EntitlementAuditDenyContext(
            ActiveTenantId,
            "tenant_user",
            EntitlementKind.Feature,
            "ADVANCED_EXPORT",
            EntitlementDenyReason.FeatureNotEnabled,
            IsTransientFailure: false,
            Source: "IntegrationTest",
            RequirementName: "FeatureReq");

        // Act
        await sink.LogDeniedAsync(denyContext, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedOutboxRequest);
        Assert.Equal("FeatureAccess", capturedOutboxRequest.EntityType);

        var payload = capturedOutboxRequest.Payload;
        Assert.Equal("FeatureAccess", payload["EntityType"]);
        
        var metadata = payload["Metadata"] as IReadOnlyDictionary<string, object?>;
        Assert.NotNull(metadata);
        Assert.Equal("Feature", metadata["entitlementKind"]);
        Assert.Equal("ADVANCED_EXPORT", metadata["code"]);
        Assert.Equal("FeatureNotEnabled", metadata["denyReason"]);
    }
}
