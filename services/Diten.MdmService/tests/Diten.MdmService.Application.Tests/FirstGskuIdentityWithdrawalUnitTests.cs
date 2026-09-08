using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FirstGskuIdentityWithdrawalUnitTests
{
    [Fact]
    public void ExactMakerWithdrawalContract_IsAccepted()
    {
        var command = new WithdrawFirstGskuIdentityApprovalCommand(new(
            Guid.NewGuid(), 1, Guid.NewGuid(), "REQUESTER_WITHDRAWAL", "No longer required"));

        var result = new WithdrawFirstGskuIdentityApprovalValidator().Validate(command);

        Assert.True(result.IsValid);
        Assert.Equal("mdm.gskus.withdraw", FirstGskuIdentityLifecyclePermissions.Withdraw);
    }

    [Theory]
    [InlineData(" REQUESTER_WITHDRAWAL")]
    [InlineData("REQUESTER_WITHDRAWAL ")]
    [InlineData("REQUESTER\nWITHDRAWAL")]
    public void NonCanonicalReason_IsRejected(string reasonCode)
    {
        var command = new WithdrawFirstGskuIdentityApprovalCommand(new(
            Guid.NewGuid(), 1, Guid.NewGuid(), reasonCode, null));

        Assert.False(new WithdrawFirstGskuIdentityApprovalValidator().Validate(command).IsValid);
    }

    [Fact]
    public void MissingOperationIdentity_IsRejected()
    {
        var command = new WithdrawFirstGskuIdentityApprovalCommand(new(
            Guid.NewGuid(), 1, Guid.Empty, "REQUESTER_WITHDRAWAL", null));

        Assert.False(new WithdrawFirstGskuIdentityApprovalValidator().Validate(command).IsValid);
    }
}
