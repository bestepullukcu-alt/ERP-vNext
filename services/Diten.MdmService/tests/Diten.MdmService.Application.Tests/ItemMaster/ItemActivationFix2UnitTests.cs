using Diten.MdmService.Application.Features.ItemMaster.Activation;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests.ItemMaster;

/// <summary>MOD-0290-FU04 S2 FIX2 — the pure parts: the refusal-repeat filter at the retry limit (X5).</summary>
public sealed class ItemActivationFix2UnitTests
{
    // X5: at the retry limit the operation's LastFailureCode is RETRY_LIMIT_REACHED and its cause is kept aside; the
    // repeat filter compares with the CAUSE — the same refusal is not recorded again, a new one is.
    [Fact]
    public void Fix2_X5_At_the_retry_limit_the_repeat_filter_compares_with_the_kept_cause()
    {
        var command = Command(new ItemWorkflowOperation
        {
            LastFailureCode = ItemActivationWorkflowProcessor.RetryLimitReached,
            RetryLimitCauseCode = "ITEM_CONCURRENCY_CONFLICT"
        });

        Assert.False(command.ShouldAuditFailure("ITEM_CONCURRENCY_CONFLICT"));
        Assert.False(command.ShouldAuditFailure("ITEM_VERSION_CONFLICT"));
        Assert.True(command.ShouldAuditFailure("PRODUCT_IDENTITY_STORE_UNAVAILABLE"));
    }

    [Fact]
    public void Fix2_X5_Below_the_limit_the_repeat_filter_compares_with_the_last_failure()
    {
        var command = Command(new ItemWorkflowOperation { LastFailureCode = "ITEM_CONCURRENCY_CONFLICT" });

        Assert.False(command.ShouldAuditFailure("ITEM_CONCURRENCY_CONFLICT"));
        Assert.True(command.ShouldAuditFailure("ITEM_ACTIVATION_STATE_CONFLICT"));
        Assert.True(Command(new ItemWorkflowOperation()).ShouldAuditFailure("ITEM_CONCURRENCY_CONFLICT"));
    }

    private static ApplyItemActivationDecisionCommand Command(ItemWorkflowOperation operation)
    {
        var id = Guid.NewGuid();
        return new(id, Guid.NewGuid(),
            new ItemWorkflowClaim(Guid.NewGuid(), Guid.NewGuid(), id, new string('a', 64), "unit", 1,
                ItemWorkflowCheckpoint.ApprovalValidated, DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks),
            operation, null, null, DateTimeOffset.UtcNow.UtcTicks);
    }
}
