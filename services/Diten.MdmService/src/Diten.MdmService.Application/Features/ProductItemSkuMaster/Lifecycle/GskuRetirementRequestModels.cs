using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class GskuRetirementRequestPermissions
{
    public const string Request = "mdm.gskus.request-retirement";
}

// FIX3 item 1: after QuarantineAfterFailedAttempts retries in a row the request goes to manual reconciliation.
public sealed record GskuRetirementRequestExecutionConfiguration(TimeSpan LeaseDuration, TimeSpan RetryDelay,
    int QuarantineAfterFailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts,
    TimeSpan? MaxRetryDelay = null,
    // FIX3 S8: how long after a start was sent the engine's "nothing arrived" is still not trusted (it may still be
    // processing the start). Longer than the start call's own timeout; default two minutes.
    TimeSpan? StartSettleWindow = null)
{
    public TimeSpan SettleWindow => StartSettleWindow is { } window && window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(2);
}
