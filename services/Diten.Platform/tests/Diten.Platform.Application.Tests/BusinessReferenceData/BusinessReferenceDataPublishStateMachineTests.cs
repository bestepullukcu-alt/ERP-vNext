using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataPublishStateMachineTests
{
    [Fact]
    public void Vocabulary_IsExact()
    {
        Assert.Equal(
            ["PENDING", "RUNNING", "RECOVERY_REQUIRED", "COMPLETED", "FAILED_TERMINAL"],
            Enum.GetNames<BusinessReferenceDataPublishOperationState>());
        Assert.Equal(
            ["INITIALIZED", "TARGET_VERSION_WRITTEN", "PRIOR_VERSIONS_DEPRECATED", "REQUIRED_WRITES_VERIFIED", "POINTER_PROMOTED", "COMPLETION_VERIFIED"],
            Enum.GetNames<BusinessReferenceDataPublishCheckpoint>());
    }

    [Theory]
    [InlineData(BusinessReferenceDataPublishCheckpoint.INITIALIZED, BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN, BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED, BusinessReferenceDataPublishCheckpoint.REQUIRED_WRITES_VERIFIED)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.REQUIRED_WRITES_VERIFIED, BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED)]
    [InlineData(BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED, BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED)]
    public void RunningCheckpoint_AdvancesExactlyOneStep(
        BusinessReferenceDataPublishCheckpoint current,
        BusinessReferenceDataPublishCheckpoint next)
    {
        Assert.True(BusinessReferenceDataPublishStateMachine.IsValidTransition(
            BusinessReferenceDataPublishOperationState.RUNNING,
            current,
            BusinessReferenceDataPublishOperationState.RUNNING,
            next));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsValidTransition(
            BusinessReferenceDataPublishOperationState.RUNNING,
            current,
            BusinessReferenceDataPublishOperationState.RUNNING,
            current));
    }

    [Fact]
    public void Recovery_ResumesSameCheckpoint_AndPostPointerCannotFailTerminal()
    {
        Assert.True(BusinessReferenceDataPublishStateMachine.IsValidTransition(
            BusinessReferenceDataPublishOperationState.PENDING,
            BusinessReferenceDataPublishCheckpoint.INITIALIZED,
            BusinessReferenceDataPublishOperationState.FAILED_TERMINAL,
            BusinessReferenceDataPublishCheckpoint.INITIALIZED));
        Assert.True(BusinessReferenceDataPublishStateMachine.IsValidTransition(
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED,
            BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED,
            BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED));
        Assert.True(BusinessReferenceDataPublishStateMachine.IsValidTransition(
            BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED,
            BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED,
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsValidTransition(
            BusinessReferenceDataPublishOperationState.RUNNING,
            BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED,
            BusinessReferenceDataPublishOperationState.FAILED_TERMINAL,
            BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED));
    }

    [Fact]
    public void ReplayIdentity_RequiresSameTenantKeyTargetAndFencingFingerprint()
    {
        var tenantId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var expectedPublishedVersionId = Guid.NewGuid();
        var existing = CreateOperation(
            tenantId,
            setId,
            versionId,
            "publish-1",
            expectedPublishedVersionId,
            expectedSetVersion: 7,
            expectedTargetVersionToken: "captured-target-token");

        Assert.True(BusinessReferenceDataPublishStateMachine.IsSameReplayTarget(
            existing,
            CreateOperation(
                tenantId,
                setId,
                versionId,
                "publish-1",
                expectedPublishedVersionId,
                expectedSetVersion: 7,
                expectedTargetVersionToken: "captured-target-token")));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsSameReplayTarget(
            existing,
            CreateOperation(tenantId, setId, Guid.NewGuid(), "publish-1", expectedPublishedVersionId, 7, "captured-target-token")));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsSameReplayTarget(
            existing,
            CreateOperation(tenantId, setId, versionId, "publish-1", Guid.NewGuid(), 7, "captured-target-token")));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsSameReplayTarget(
            existing,
            CreateOperation(tenantId, setId, versionId, "publish-1", expectedPublishedVersionId, 8, "captured-target-token")));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsSameReplayTarget(
            existing,
            CreateOperation(tenantId, setId, versionId, "publish-1", expectedPublishedVersionId, 7, "different-target-token")));
    }

    [Fact]
    public void VerifiedPublication_RequiresTerminalAgreement()
    {
        var operation = CreateOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "publish-1");
        operation.OperationState = BusinessReferenceDataPublishOperationState.COMPLETED;
        operation.PublishCheckpoint = BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED;
        operation.PreMutationContextVerifiedAt = DateTimeOffset.UtcNow;
        operation.CompletedAt = DateTimeOffset.UtcNow;

        Assert.False(BusinessReferenceDataPublishStateMachine.IsVerifiedPublication(
            operation, Guid.NewGuid(), operation.ExpectedSetVersion + 1, "post-token", true));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsVerifiedPublication(
            operation, operation.BusinessReferenceDataVersionId, operation.ExpectedSetVersion, "post-token", true));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsVerifiedPublication(
            operation, operation.BusinessReferenceDataVersionId, operation.ExpectedSetVersion + 1, operation.ExpectedTargetVersionToken, true));
        Assert.False(BusinessReferenceDataPublishStateMachine.IsVerifiedPublication(
            operation, operation.BusinessReferenceDataVersionId, operation.ExpectedSetVersion + 1, "post-token", false));
        Assert.True(BusinessReferenceDataPublishStateMachine.IsVerifiedPublication(
            operation, operation.BusinessReferenceDataVersionId, operation.ExpectedSetVersion + 1, "post-token", true));

        operation.OperationState = BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED;
        Assert.False(BusinessReferenceDataPublishStateMachine.IsVerifiedPublication(
            operation, operation.BusinessReferenceDataVersionId, operation.ExpectedSetVersion + 1, "post-token", true));
    }

    private static BusinessReferenceDataPublishOperation CreateOperation(
        Guid tenantId,
        Guid setId,
        Guid versionId,
        string key,
        Guid? expectedPublishedVersionId = null,
        long expectedSetVersion = 1,
        string expectedTargetVersionToken = "target-token")
    {
        return new BusinessReferenceDataPublishOperation
        {
            TenantId = tenantId,
            BusinessReferenceDataSetId = setId,
            BusinessReferenceDataVersionId = versionId,
            IdempotencyKey = key,
            ExpectedPublishedVersionId = expectedPublishedVersionId,
            ExpectedSetVersion = expectedSetVersion,
            ExpectedTargetVersionToken = expectedTargetVersionToken,
            CreatedBy = "test"
        };
    }
}
