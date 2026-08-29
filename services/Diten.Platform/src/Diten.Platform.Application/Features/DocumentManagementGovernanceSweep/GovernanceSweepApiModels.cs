using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementGovernanceSweep;

// MOD-0029-FU32 — API-facing models, sweep keys and reason codes for the background governance sweep surface.
// Nothing here mutates a subject; these are inputs and read/summary projections.

public static class GovernanceSweepReasonCodes
{
    public const string RunNotFound = "GOVERNANCE_SWEEP_RUN_NOT_FOUND";
    public const string TenantRequired = "GOVERNANCE_SWEEP_TENANT_REQUIRED";
    public const string Failed = "GOVERNANCE_SWEEP_FAILED";
    public const string Unsupported = "GOVERNANCE_SWEEP_UNSUPPORTED";
    public const string DryRun = "GOVERNANCE_SWEEP_DRY_RUN";
    public const string PartialFailure = "GOVERNANCE_SWEEP_PARTIAL_FAILURE";
}

/// <summary>
/// MOD-0029-FU32 — the stable machine keys of the sweep groups. A key is part of the persisted evidence, so it is
/// never renamed; a behavioural change bumps <see cref="DocumentGovernanceSweepCatalog.SweepVersion"/> instead.
/// </summary>
public static class DocumentGovernanceSweepKeys
{
    public const string RunAll = "document-governance.run-all";
    public const string PeriodicReviews = "document-governance.periodic-reviews";
    public const string ExternalDocuments = "document-governance.external-documents";
    public const string TemporaryInstructions = "document-governance.temporary-instructions";
    public const string DowntimeTemporaryIssues = "document-governance.downtime-temporary-issues";
    public const string QualityCapa = "document-governance.quality-capa";
    public const string SignatureRequests = "document-governance.signature-requests";
    public const string RetentionEligibility = "document-governance.retention-eligibility";
    public const string LegalHoldScope = "document-governance.legal-hold-scope";
}

/// <summary>MOD-0029-FU32 — the sweep catalog: display names and the version stamped on every run row.</summary>
public static class DocumentGovernanceSweepCatalog
{
    /// <summary>Bumped whenever a sweep group's evaluation semantics change.</summary>
    public const string SweepVersion = "1.0.0";

    public const string SopReference = "GMG-QMS-SOP-0001";

    /// <summary>The groups a <c>run-all</c> executes, in order.</summary>
    public static readonly IReadOnlyList<string> RunAllGroups =
    [
        DocumentGovernanceSweepKeys.PeriodicReviews,
        DocumentGovernanceSweepKeys.ExternalDocuments,
        DocumentGovernanceSweepKeys.TemporaryInstructions,
        DocumentGovernanceSweepKeys.DowntimeTemporaryIssues,
        DocumentGovernanceSweepKeys.QualityCapa,
        DocumentGovernanceSweepKeys.SignatureRequests,
        DocumentGovernanceSweepKeys.RetentionEligibility,
        DocumentGovernanceSweepKeys.LegalHoldScope
    ];

    public static string NameOf(string sweepKey) => sweepKey switch
    {
        DocumentGovernanceSweepKeys.RunAll => "All Document Governance Sweeps",
        DocumentGovernanceSweepKeys.PeriodicReviews => "Periodic Review Overdue Sweep",
        DocumentGovernanceSweepKeys.ExternalDocuments => "External Document Monitoring & Impact Sweep",
        DocumentGovernanceSweepKeys.TemporaryInstructions => "Temporary Instruction Expiry Sweep",
        DocumentGovernanceSweepKeys.DowntimeTemporaryIssues => "Downtime Temporary Issue Reconciliation Sweep",
        DocumentGovernanceSweepKeys.QualityCapa => "Quality Event / CAPA Overdue Sweep",
        DocumentGovernanceSweepKeys.SignatureRequests => "Signature Request Expiry Sweep",
        DocumentGovernanceSweepKeys.RetentionEligibility => "Retention Eligibility Sweep",
        DocumentGovernanceSweepKeys.LegalHoldScope => "Legal Hold Scope Freshness Sweep",
        _ => sweepKey
    };

    public static bool IsKnown(string sweepKey) =>
        sweepKey == DocumentGovernanceSweepKeys.RunAll || RunAllGroups.Contains(sweepKey);
}

/// <summary>
/// MOD-0029-FU32 — the manual-trigger request body. It carries NO TenantId: the tenant is resolved server-side.
/// </summary>
/// <param name="DryRun">
/// When true the sweep evaluates and reports but writes absolutely nothing — no escalation, no finding, no subject
/// mutation and no run-history row.
/// </param>
/// <param name="AsOfDate">
/// Server-validated evaluation instant for CANDIDATE SELECTION. Report-only groups honour it exactly; groups that
/// delegate to a pre-existing FU12/FU13/FU20 evaluator warn, because those evaluators time-stamp with UtcNow.
/// </param>
/// <param name="MaxItems">Caps the scanned subjects per group. A truncated group warns.</param>
/// <param name="SweepKeys">Restricts a run-all to a subset of groups. Ignored by the single-group endpoints.</param>
public sealed record GovernanceSweepRunInput(
    bool DryRun = false,
    DateTimeOffset? AsOfDate = null,
    int? MaxItems = null,
    IReadOnlyList<string>? SweepKeys = null);

/// <summary>MOD-0029-FU32 — one per-subject result line on the response.</summary>
public sealed record GovernanceSweepResultItemModel(
    string SubjectType,
    Guid SubjectId,
    string Action,
    DocumentGovernanceSweepItemOutcome Outcome,
    string? Message,
    Guid? RelatedFindingId,
    Guid? RelatedEscalationId);

/// <summary>MOD-0029-FU32 — the per-group summary inside a run.</summary>
public sealed record GovernanceSweepGroupSummaryModel(
    string SweepKey,
    string SweepName,
    int ItemsScanned,
    int ItemsAffected,
    int EscalationsCreated,
    int ExistingEscalationsSkipped,
    int FindingsCreated,
    int ExistingFindingsSkipped,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<GovernanceSweepResultItemModel> Items);

/// <summary>
/// MOD-0029-FU32 — the outcome of one sweep run. <c>RunId</c> is <see cref="Guid.Empty"/> for a dry run, which
/// writes no history row.
/// </summary>
public sealed record GovernanceSweepRunModel(
    Guid RunId,
    string SweepKey,
    string SweepName,
    string SweepVersion,
    DocumentGovernanceSweepTriggerType TriggerType,
    DocumentGovernanceSweepStatus Status,
    bool DryRun,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset AsOfDate,
    int ItemsScanned,
    int ItemsAffected,
    int FindingsCreated,
    int EscalationsCreated,
    int ExistingFindingsSkipped,
    int ExistingEscalationsSkipped,
    IReadOnlyList<string> SweepKeysExecuted,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage,
    IReadOnlyList<GovernanceSweepGroupSummaryModel> Groups);

/// <summary>MOD-0029-FU32 — run-history list row.</summary>
public sealed record GovernanceSweepRunSummaryModel(
    Guid Id,
    string SweepKey,
    string SweepName,
    string SweepVersion,
    DocumentGovernanceSweepTriggerType TriggerType,
    DocumentGovernanceSweepStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int ItemsScanned,
    int ItemsAffected,
    int EscalationsCreated,
    int FindingsCreated,
    string? CreatedBy);

/// <summary>MOD-0029-FU32 — full run-history detail, including every result line.</summary>
public sealed record GovernanceSweepRunDetailModel(
    Guid Id,
    string SweepKey,
    string SweepName,
    string SweepVersion,
    DocumentGovernanceSweepTriggerType TriggerType,
    DocumentGovernanceSweepStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset AsOfDate,
    Guid? TriggeredByUserId,
    string? CreatedBy,
    string? CorrelationId,
    int ItemsScanned,
    int ItemsAffected,
    int FindingsCreated,
    int EscalationsCreated,
    int ExistingFindingsSkipped,
    int ExistingEscalationsSkipped,
    IReadOnlyList<string> SweepKeysExecuted,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage,
    bool DryRun,
    IReadOnlyList<GovernanceSweepResultItemModel> ResultItems);
