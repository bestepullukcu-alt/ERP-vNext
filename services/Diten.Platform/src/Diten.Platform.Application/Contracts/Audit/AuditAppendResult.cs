namespace Diten.Platform.Application.Contracts.Audit;

public sealed class AuditAppendResult
{
    private AuditAppendResult(
        AuditAppendStatus status,
        string? idempotencyKey,
        string? diagnostic,
        bool shouldBreakBusinessCommand)
    {
        Status = status;
        IdempotencyKey = idempotencyKey;
        Diagnostic = diagnostic;
        ShouldBreakBusinessCommand = shouldBreakBusinessCommand;
    }

    public AuditAppendStatus Status { get; }
    public string? IdempotencyKey { get; }
    public string? Diagnostic { get; }
    public bool ShouldBreakBusinessCommand { get; }
    public bool IsEnqueued => Status == AuditAppendStatus.Queued;
    public bool IsDuplicate => Status == AuditAppendStatus.Duplicate;
    public bool IsControlledFailure => Status is AuditAppendStatus.Duplicate
        or AuditAppendStatus.EnqueueFailed
        or AuditAppendStatus.SkippedRecursion
        or AuditAppendStatus.Rejected;

    public static AuditAppendResult Queued(string idempotencyKey)
    {
        return new AuditAppendResult(AuditAppendStatus.Queued, idempotencyKey, null, shouldBreakBusinessCommand: false);
    }

    public static AuditAppendResult Duplicate(string idempotencyKey)
    {
        return new AuditAppendResult(AuditAppendStatus.Duplicate, idempotencyKey, "Duplicate audit enqueue request.", shouldBreakBusinessCommand: false);
    }

    public static AuditAppendResult SkippedRecursion(string diagnostic)
    {
        return new AuditAppendResult(AuditAppendStatus.SkippedRecursion, null, diagnostic, shouldBreakBusinessCommand: false);
    }

    /// <summary>
    /// The audit request itself was malformed (missing correlation id, request type, entity type, etc.)
    /// or could not resolve required ambient context (tenant id, actor).
    /// </summary>
    /// <remarks>
    /// Default behavior: <see cref="ShouldBreakBusinessCommand"/> is <c>false</c>.
    /// Audit instrumentation defects must NOT break business commands by default
    /// (pack invariant: "Audit enqueue failure must not unnecessarily break the business command").
    /// Use <see cref="RejectedCritical"/> only for explicit critical audit categories
    /// where a failed audit append must abort the business command (e.g., regulated
    /// security-sensitive operations approved by implementation notes).
    /// </remarks>
    public static AuditAppendResult Rejected(string diagnostic)
    {
        return new AuditAppendResult(AuditAppendStatus.Rejected, null, diagnostic, shouldBreakBusinessCommand: false);
    }

    /// <summary>
    /// Rejection variant for explicit critical audit categories. The caller (typically
    /// <c>AuditBehavior</c>) is expected to abort the business command when this result is returned.
    /// </summary>
    /// <remarks>
    /// MUST only be produced for commands that belong to a critical audit category
    /// explicitly approved in implementation notes / <c>AuditBehaviorOptions.CriticalCategories</c>.
    /// Default audit instrumentation MUST NOT use this factory. Initial whitelist is empty.
    /// </remarks>
    public static AuditAppendResult RejectedCritical(string diagnostic)
    {
        return new AuditAppendResult(AuditAppendStatus.Rejected, null, diagnostic, shouldBreakBusinessCommand: true);
    }

    public static AuditAppendResult EnqueueFailed(string? idempotencyKey, string diagnostic)
    {
        return new AuditAppendResult(AuditAppendStatus.EnqueueFailed, idempotencyKey, diagnostic, shouldBreakBusinessCommand: false);
    }
}
