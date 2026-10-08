namespace Diten.InventoryService.Application.Features.Quarantine;

/// <summary>
/// QUARANTINE-BUNDLE (MOD-0175) wire constants — contract <c>quarantine.openapi.yaml</c> 1.0.0
/// (frozen from the CT-accepted proposal <c>drafts/quarantine-bundle-v1-proposal.md</c>, slice Q1).
/// </summary>
public static class QuarantineContract
{
    public const string BasePath = "/api/quarantine";

    public const string ModuleId = "MOD-0175";

    /// <summary>Self-registration module code (pack §14, CT-Q12: domain practice, upper case).</summary>
    public const string ModuleCode = "QUARANTINE-BLOCKED-STOCK";

    /// <summary>Workflow template of every decision (CT-Q1) — used from slice Q2.</summary>
    public const string WorkflowTemplateCode = "INV-QUARANTINE-RELEASE";

    // Ledger movement envelope of every 0175 posting (pack §8): sourceModule + sourceType.
    public const string SourceTypeDecision = "QUARANTINE_DECISION";
    public const string SourceTypeHold = "QUARANTINE_HOLD";
    public const string SourceTypeBlock = "QUARANTINE_BLOCK";

    // Record numbers (pack §3).
    public const string HoldPrefix = "KRN";
    public const string DecisionPrefix = "QKR";
    public const string BlockedPrefix = "BLK";

    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 50;
    public const int MaxNoteLength = 2000;
    public const int MaxTextLength = 200;
    public const int MinIdempotencyKeyLength = 8;
    public const int MaxIdempotencyKeyLength = 100;
    public const int MinApproverCandidates = 1;
    public const int MaxApproverCandidates = 10;
    public const int MinWaitTargetDays = 1;
    public const int MaxWaitTargetDays = 60;
    public const int ExpirySoonDays = 90;

    /// <summary>The seven tenant languages of a block-reason name (CT-Q8).</summary>
    public static readonly IReadOnlyList<string> Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
}

/// <summary>QUARANTINE-BUNDLE v1 error codes (contract §4.24) that are not decision-rule codes (those live in the Domain rules).</summary>
public static class QuarantineErrorCodes
{
    public const string HoldNotFound = "QUARANTINE_HOLD_NOT_FOUND";
    public const string BlockedNotFound = "QUARANTINE_BLOCKED_NOT_FOUND";
    public const string DecisionNotFound = "QUARANTINE_DECISION_NOT_FOUND";
    public const string Conflict = "QUARANTINE_CONFLICT";
    public const string DecisionNotDraft = "QUARANTINE_DECISION_NOT_DRAFT";
    public const string DecisionNotPending = "QUARANTINE_DECISION_NOT_PENDING";
    public const string DecisionNotApplicable = "QUARANTINE_DECISION_NOT_APPLICABLE";
    public const string NotDecider = "QUARANTINE_NOT_DECIDER";
    public const string StockReserved = "QUARANTINE_STOCK_RESERVED";
    public const string NothingAvailable = "QUARANTINE_NOTHING_AVAILABLE";
    public const string WorkflowUnavailable = "QUARANTINE_WORKFLOW_UNAVAILABLE";
    public const string BlockReasonLanguageMissing = "QUARANTINE_BLOCK_REASON_LANGUAGE_MISSING";
    public const string BlockReasonExists = "QUARANTINE_BLOCK_REASON_EXISTS";
    public const string BlockReasonInUse = "QUARANTINE_BLOCK_REASON_IN_USE";
    public const string SignatureRequired = "QUARANTINE_SIGNATURE_REQUIRED";
}
