using Diten.MdmService.Application.Contracts.Workflow;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>Which remote call produced the answer that is being classified.</summary>
public enum ProductIdentityWorkflowCall
{
    Start,
    StartResult,
    TerminalEvidence,
    CancellationPreflight,
    Cancel,
    // The reference-data service a GSKU's approval revalidates against.
    ReferenceData
}

public enum ProductIdentityWorkflowFaultKind
{
    /// <summary>The service chain is at fault (credential, token, setting, reachability). The record waits.</summary>
    ServiceWait,

    /// <summary>Only the maker can continue: the record waits for a resubmit and the maker is told why.</summary>
    MakerReplay,

    /// <summary>The record itself is at fault and repeating the call will not help.</summary>
    Quarantine
}

/// <summary>How far a service fault reaches — what the recovery cycle may skip without calling again.</summary>
public enum ProductIdentityWorkflowFaultScope
{
    None = 0,
    Tenant = 1,
    AllTenants = 2
}

/// <param name="StartedNothing">
/// True when the answer proves the remote side did not act on the request (rejected before it was processed).
/// False when the outcome is unknown (no answer, unreadable answer).
/// </param>
/// <param name="MakerCode">The stable code the maker receives; only for <see cref="ProductIdentityWorkflowFaultKind.MakerReplay"/>.</param>
/// <param name="Loud">The fault is logged as an error on every attempt, not only after a threshold.</param>
public sealed record ProductIdentityWorkflowFaultDecision(
    ProductIdentityWorkflowFaultKind Kind,
    ProductIdentityWorkflowFaultScope Scope = ProductIdentityWorkflowFaultScope.None,
    bool StartedNothing = false,
    string? MakerCode = null,
    bool Loud = false);

/// <summary>
/// The codes a maker receives when an approval submit or withdrawal cannot continue without them. They are a
/// contract with the screen (the sentences are GP-2's work): one place, never renamed.
/// </summary>
public static class ProductIdentityWorkflowMakerCodes
{
    /// <summary>The maker's session was not accepted by the approval engine — sign in again and resubmit.</summary>
    public const string SessionExpired = "PRODUCT_IDENTITY_WORKFLOW_SESSION_EXPIRED";

    /// <summary>The maker has no permission to start an approval.</summary>
    public const string StartPermissionRequired = "PRODUCT_IDENTITY_WORKFLOW_START_PERMISSION_REQUIRED";

    /// <summary>The approval step has nobody assigned to it.</summary>
    public const string ApproverNotAssigned = "PRODUCT_IDENTITY_WORKFLOW_APPROVER_NOT_ASSIGNED";

    /// <summary>
    /// WP-MDM-NO-ELIGIBLE-APPROVER-01 — segregation of duties: the maker would be the approval's only eligible approver
    /// (Platform WORKFLOW_NO_ELIGIBLE_APPROVER on the start). Nothing was started; an administrator must adjust the
    /// approvers before the maker submits again.
    /// </summary>
    public const string NoEligibleApprover = "PRODUCT_IDENTITY_WORKFLOW_NO_ELIGIBLE_APPROVER";

    /// <summary>The approval definition is missing or not published.</summary>
    public const string DefinitionNotReady = "PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY";

    /// <summary>A service connection problem: the draft is intact, the maker resubmits.</summary>
    public const string ServiceUnavailableResubmit = "PRODUCT_IDENTITY_WORKFLOW_SERVICE_UNAVAILABLE_RESUBMIT";

    /// <summary>The start never reached the approval engine and nobody but the maker can repeat it.</summary>
    public const string MakerReplayRequired = "PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED";

    /// <summary>The approval kept changing while it was being withdrawn (still open): ask again in a moment.</summary>
    public const string ApprovalChanging = "PRODUCT_IDENTITY_WORKFLOW_APPROVAL_CHANGING";

    /// <summary>The approval was decided or closed before the withdrawal reached it, and the engine named no reason.</summary>
    public const string ApprovalClosed = "PRODUCT_IDENTITY_WITHDRAWAL_APPROVAL_CLOSED";

    public static int StatusFor(string? code) => code switch
    {
        SessionExpired => 401,
        StartPermissionRequired => 403,
        ServiceUnavailableResubmit => 503,
        _ => 409
    };

    public static bool IsMakerCode(string? code) => code is SessionExpired or StartPermissionRequired
        or ApproverNotAssigned or DefinitionNotReady or ServiceUnavailableResubmit or MakerReplayRequired
        or ApprovalChanging or NoEligibleApprover;
}

/// <summary>
/// One classification of what the approval transport can answer, for every product-identity processor. A fault of
/// the service identity, of a setting or of reachability is not a fault of the business record: it waits. Manual
/// reconciliation is only for an answer that is about the record and that repeating will not change.
/// Successful and "not yet" answers (Success, NonTerminal, Incomplete, and NotFound outside a start) are the
/// caller's normal flow and are not classified here.
/// </summary>
public static class ProductIdentityWorkflowFaultPolicy
{
    public const string ResponseUnreadable = "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE";
    /// <summary>The engine answered "done" and what it sent does not fit this service's contract.</summary>
    public const string ResponseInvalid = "PRODUCT_WORKFLOW_RESPONSE_INVALID";
    /// <summary>A failure status whose envelope names no reason.</summary>
    public const string FailureWithoutReason = "PRODUCT_WORKFLOW_FAILURE_WITHOUT_REASON";
    public const string IdentityNotGranted = "PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED";

    /*
     * FIX3 E1 (CT decision 3): the engine's terminal evidence names an approval CLOSED WITHOUT A DECISION (cancelled,
     * timed out, closed undecided) with its own reason. Only that answer sends a request back (to draft / frees the
     * GSKU); any other conflict (a version, a contradiction between the engine's own facts) is quarantined as before.
     * FIX5: Platform names it since BL-547 (GetTrustedWorkflowTerminalDecisionEvidenceHandler — cancelled or timed out
     * without a decision); an escalated approval that is still open is WORKFLOW_DECISION_NOT_TERMINAL (it waits), and
     * WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT is only evidence that contradicts itself.
     */
    public const string ClosedWithoutDecision = "WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION";

    public static bool IsClosedWithoutDecision<T>(ProductIdentityWorkflowTransportResult<T> result) =>
        result.Outcome == ProductIdentityWorkflowTransportOutcome.Conflict && result.Value is null
        && result.ErrorCode == ClosedWithoutDecision;
    private const string DelegatedUserUnauthenticated = "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED";
    private const string DelegatedUserForbidden = "WORKFLOW_DELEGATED_USER_FORBIDDEN";
    private const string ConsumerForbidden = "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN";
    private const string TenantMismatch = "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH";
    private const string CancelRequesterForbidden = "WORKFLOW_TRUSTED_CANCEL_REQUESTER_FORBIDDEN";
    private const string CandidatesRequired = "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED";
    // Platform (WorkflowModels.WorkflowNoEligibleApprover): the start's only candidate is the maker (SoD) — refused 400
    // before anything is written. Named apart from the maker code it becomes.
    private const string PlatformNoEligibleApprover = "WORKFLOW_NO_ELIGIBLE_APPROVER";

    private static readonly ProductIdentityWorkflowFaultDecision Quarantine =
        new(ProductIdentityWorkflowFaultKind.Quarantine);

    public static ProductIdentityWorkflowFaultDecision Classify(
        ProductIdentityWorkflowCall call,
        ProductIdentityWorkflowTransportOutcome outcome,
        string? errorCode) => outcome switch
    {
        ProductIdentityWorkflowTransportOutcome.Retryable or ProductIdentityWorkflowTransportOutcome.Timeout =>
            new(ProductIdentityWorkflowFaultKind.ServiceWait),

        // 401. Only the maker's own session is the maker's to fix; every other refusal is the service identity
        // (unknown codes included). A missing tenant grant stops one tenant, a refused credential or token all.
        ProductIdentityWorkflowTransportOutcome.AuthenticationRejected => errorCode switch
        {
            DelegatedUserUnauthenticated => Maker(ProductIdentityWorkflowMakerCodes.SessionExpired),
            IdentityNotGranted => new(ProductIdentityWorkflowFaultKind.ServiceWait,
                ProductIdentityWorkflowFaultScope.Tenant, StartedNothing: true),
            _ => new(ProductIdentityWorkflowFaultKind.ServiceWait,
                ProductIdentityWorkflowFaultScope.AllTenants, StartedNothing: true)
        },

        // 403. Two answers are about the record (who asks, for which tenant); one is the maker's permission; the
        // rest — unknown codes included — is how the engine treats this service.
        ProductIdentityWorkflowTransportOutcome.Forbidden => errorCode switch
        {
            DelegatedUserForbidden => Maker(ProductIdentityWorkflowMakerCodes.StartPermissionRequired),
            TenantMismatch or CancelRequesterForbidden => Quarantine,
            ConsumerForbidden => new(ProductIdentityWorkflowFaultKind.ServiceWait,
                ProductIdentityWorkflowFaultScope.AllTenants, StartedNothing: true),
            _ => new(ProductIdentityWorkflowFaultKind.ServiceWait, StartedNothing: true)
        },

        // 400 and contract failures. An answer nobody could read says nothing about the record.
        ProductIdentityWorkflowTransportOutcome.Invalid => errorCode switch
        {
            ResponseUnreadable => new(ProductIdentityWorkflowFaultKind.ServiceWait),
            // A success this service could not accept (a field it does not know, a contradiction) is a mismatch of
            // VERSIONS, not a fault of the record — and the engine may well have done what was asked. The outcome
            // is unknown: wait and ask again, and say so loudly at once, because waiting will not fix it.
            ResponseInvalid => new(ProductIdentityWorkflowFaultKind.ServiceWait, Loud: true),
            CandidatesRequired => Maker(ProductIdentityWorkflowMakerCodes.ApproverNotAssigned),
            // WP-MDM-NO-ELIGIBLE-APPROVER-01: only a START can meet it (at a decision Platform answers 409 and MDM does
            // not decide); elsewhere it stays what any other record answer is.
            PlatformNoEligibleApprover when call == ProductIdentityWorkflowCall.Start =>
                Maker(ProductIdentityWorkflowMakerCodes.NoEligibleApprover),
            _ => Quarantine
        },

        // 409. A definition that is not ready is set-up, and only a start can meet it.
        ProductIdentityWorkflowTransportOutcome.Conflict => call == ProductIdentityWorkflowCall.Start
            && errorCode is "WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION" or "WORKFLOW_TEMPLATE_VERSION_NOT_FOUND"
                or "WORKFLOW_TEMPLATE_NOT_PUBLISHED"
            ? Maker(ProductIdentityWorkflowMakerCodes.DefinitionNotReady)
            : Quarantine,

        // 404 on a start is "no such definition"; elsewhere the caller decides what an absent thing means.
        ProductIdentityWorkflowTransportOutcome.NotFound when call == ProductIdentityWorkflowCall.Start =>
            Maker(ProductIdentityWorkflowMakerCodes.DefinitionNotReady),

        _ => Quarantine
    };

    /// <summary>Doubles from <paramref name="retryDelay"/> per consecutive fault, never above the ceiling.</summary>
    public static TimeSpan Backoff(int consecutiveFaults, TimeSpan retryDelay, TimeSpan ceiling)
    {
        if (retryDelay <= TimeSpan.Zero) return ceiling;
        if (retryDelay >= ceiling) return ceiling;
        var delay = retryDelay;
        for (var attempt = 1; attempt < consecutiveFaults; attempt++)
        {
            delay += delay;
            if (delay >= ceiling) return ceiling;
        }
        return delay;
    }

    private static ProductIdentityWorkflowFaultDecision Maker(string code) =>
        new(ProductIdentityWorkflowFaultKind.MakerReplay, StartedNothing: true, MakerCode: code);
}

/// <summary>Read from configuration: how long a waiting record may back off, and when waiting becomes an error.</summary>
public sealed record ProductIdentityWorkflowServiceFaultSettings(TimeSpan MaxRetryDelay, int ErrorAfterAttempts)
{
    // BL-552 (read by the Global Product identity flow only): how long after a start "nothing arrived" is still not
    // trusted, and how many worker retries in a row on an answer about the record before a person is needed.
    public TimeSpan StartSettleWindow { get; init; } = TimeSpan.FromMinutes(2);
    public int QuarantineAfterFailedAttempts { get; init; } = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts;

    public static ProductIdentityWorkflowServiceFaultSettings Default { get; } = new(TimeSpan.FromMinutes(15), 5);
}
