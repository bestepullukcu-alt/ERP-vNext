namespace Diten.Platform.Application.Features.Notifications.BackgroundJobs;

public sealed record EmailDispatchJobArgs(
    Guid TenantId,
    Guid DispatchId,
    /// <summary>
    /// BL-406 — the SAME maxRetryCount the sweep used to select this dispatch (<c>EmailDispatchSweepJob</c>'s
    /// own <c>DefaultMaxRetryCount</c> unless overridden by that sweep's own args). Threaded through so this job
    /// can tell whether the attempt it is about to make, if it fails, is the PERMANENT failure (no further
    /// retry will ever be due) without duplicating the sweep's own default elsewhere. Defaulted to 5 — the
    /// existing, unchanged <c>EmailDispatchSweepJob.DefaultMaxRetryCount</c> — only so the many existing
    /// 2-argument test call sites (<c>new EmailDispatchJobArgs(tenantId, dispatchId)</c>) keep compiling; the
    /// sweep itself always passes its own resolved value explicitly.
    /// </summary>
    int MaxRetryCount = 5);
