using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuIdentityApprovalWithdrawalUnitTests
{
    [Fact]
    public async Task Exact_maker_withdrawal_contract_is_accepted()
    {
        var command = new WithdrawLskuIdentityApprovalCommand(new(
            Guid.NewGuid(), 2, Guid.NewGuid(), "REQUESTER_WITHDRAWAL", "Duplicate submission"));
        var result = await new WithdrawLskuIdentityApprovalValidator().ValidateAsync(command);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(" REQUESTER_WITHDRAWAL")]
    [InlineData("REQUESTER_WITHDRAWAL ")]
    [InlineData("REQUESTER\nWITHDRAWAL")]
    public async Task Non_canonical_reason_is_rejected(string reason)
    {
        var command = new WithdrawLskuIdentityApprovalCommand(new(
            Guid.NewGuid(), 2, Guid.NewGuid(), reason, null));
        var result = await new WithdrawLskuIdentityApprovalValidator().ValidateAsync(command);
        Assert.False(result.IsValid);
    }
}
