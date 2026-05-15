using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditPersistenceFoundationTests
{
    [Fact]
    public void RetentionPolicy_ShouldRejectZeroRetentionFloor()
    {
        var policy = CreateValidPolicy();
        policy.MinimumRetentionDays = 0;

        Assert.Throws<InvalidOperationException>(() => policy.Validate());
    }

    [Fact]
    public void RetentionPolicy_ShouldRejectDefaultOutsideFloorAndCeiling()
    {
        var policy = CreateValidPolicy();
        policy.DefaultRetentionDays = policy.MaximumRetentionDays + 1;

        Assert.Throws<InvalidOperationException>(() => policy.Validate());
    }

    [Fact]
    public void RetentionPolicy_ShouldRejectHotStorageLongerThanDefaultRetention()
    {
        var policy = CreateValidPolicy();
        policy.HotStorageDays = policy.DefaultRetentionDays + 1;

        Assert.Throws<InvalidOperationException>(() => policy.Validate());
    }

    [Fact]
    public void TenantAuditPreference_ShouldRejectRetentionOutsidePolicyBounds()
    {
        var policy = CreateValidPolicy();
        var preference = new TenantAuditPreference
        {
            TenantId = Guid.NewGuid(),
            Category = policy.Category,
            RetentionDays = policy.MaximumRetentionDays + 1,
            UpdatedByActorId = Guid.NewGuid()
        };

        Assert.Throws<InvalidOperationException>(() => preference.ValidateAgainst(policy));
    }

    [Fact]
    public void AuditEventRepositoryContract_ShouldNotExposeMutationSurface()
    {
        var methodNames = typeof(IAuditEventRepository)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        Assert.Contains(nameof(IAuditEventRepository.AppendAsync), methodNames);
        Assert.Contains(nameof(IAuditEventRepository.GetByIdAsync), methodNames);
        Assert.Contains(nameof(IAuditEventRepository.GetByIdForPlatformCrossTenantAsync), methodNames);
        Assert.DoesNotContain("UpdateAsync", methodNames);
        Assert.DoesNotContain("DeleteAsync", methodNames);
        Assert.DoesNotContain("BulkDeleteAsync", methodNames);
        Assert.DoesNotContain("HardDeleteAsync", methodNames);
    }

    [Fact]
    public void AuditEvent_ShouldRejectInsertAsDeleted()
    {
        var auditEvent = CreateValidAuditEvent(withDeletedFlag: true);

        Assert.Throws<InvalidOperationException>(() => auditEvent.ValidateAppend());
    }

    [Fact]
    public void AuditEvent_ShouldRejectEmptyCorrelationId()
    {
        var auditEvent = CreateValidAuditEvent(correlationId: Guid.Empty);

        Assert.Throws<InvalidOperationException>(() => auditEvent.ValidateAppend());
    }

    [Fact]
    public void AuditEvent_ShouldRejectEmptyEntityType()
    {
        var auditEvent = CreateValidAuditEvent(entityType: string.Empty);

        Assert.Throws<InvalidOperationException>(() => auditEvent.ValidateAppend());
    }

    [Fact]
    public void AuditEvent_ShouldRejectUnknownCategory()
    {
        var auditEvent = CreateValidAuditEvent(category: AuditCategory.Unknown);

        Assert.Throws<InvalidOperationException>(() => auditEvent.ValidateAppend());
    }

    [Fact]
    public void AuditEvent_ShouldRejectUnknownOperation()
    {
        var auditEvent = CreateValidAuditEvent(operation: AuditOperation.Unknown);

        Assert.Throws<InvalidOperationException>(() => auditEvent.ValidateAppend());
    }

    [Fact]
    public void AuditEvent_ShouldRejectUpdatedMetadata()
    {
        var auditEvent = CreateValidAuditEvent(updatedAt: DateTimeOffset.UtcNow, updatedBy: "review-test");

        Assert.Throws<InvalidOperationException>(() => auditEvent.ValidateAppend());
    }

    [Fact]
    public void AuditEvent_ShouldRejectEmptyTenantId()
    {
        var auditEvent = CreateValidAuditEvent(tenantId: Guid.Empty);

        Assert.Throws<InvalidOperationException>(() => auditEvent.ValidateAppend());
    }

    [Fact]
    public void PlatformSystemTenantId_ShouldBeReservedNonEmptyAuditTenant()
    {
        Assert.NotEqual(Guid.Empty, AuditTenantIds.PlatformSystemTenantId);
    }

    [Fact]
    public void AuditOperationDelete_ShouldRepresentBusinessEntityLifecycle()
    {
        Assert.Equal(3, (int)AuditOperation.Delete);
    }

    private static AuditEventRetentionPolicy CreateValidPolicy()
    {
        return new AuditEventRetentionPolicy
        {
            Category = AuditCategory.PlatformConfiguration,
            PlanTierCode = AuditEventRetentionPolicy.DefaultPlanTierCode,
            MinimumRetentionDays = 30,
            DefaultRetentionDays = 730,
            MaximumRetentionDays = 2555,
            HotStorageDays = 90,
            AllowTenantOverride = true,
            IsActive = true
        };
    }

    private static AuditEvent CreateValidAuditEvent(
        Guid? tenantId = null,
        Guid? correlationId = null,
        AuditCategory category = AuditCategory.PlatformConfiguration,
        string entityType = "PlatformSetting",
        AuditOperation operation = AuditOperation.Update,
        DateTimeOffset? updatedAt = null,
        string? updatedBy = null,
        bool withDeletedFlag = false)
    {
        return new AuditEvent
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            CorrelationId = correlationId ?? Guid.NewGuid(),
            Category = category,
            EntityType = entityType,
            Operation = operation,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
            IsDeleted = withDeletedFlag
        };
    }
}
