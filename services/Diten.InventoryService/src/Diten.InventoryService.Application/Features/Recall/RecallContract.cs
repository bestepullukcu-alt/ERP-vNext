namespace Diten.InventoryService.Application.Features.Recall;

/// <summary>
/// RECALL-BUNDLE v1.0.0 (<c>docs/analysis/contracts/recall.openapi.yaml</c>, owner MOD-0177) — the constants the R1 slice serves: base path,
/// idempotency-key bounds, paging, the audit actions of pack §8.12 (R1 part) and the signable-action module (R4 registers it).
/// </summary>
public static class RecallContract
{
    public const string ModuleId = "MOD-0177";

    public const string BasePath = "/api/recall";

    public const int MinIdempotencyKeyLength = 8;
    public const int MaxIdempotencyKeyLength = 100;

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    public const int MaxMarksPerRequest = 500;
    public const int MaxCustomerRefLength = 200;
    public const int MaxDocumentIdLength = 100;
    public const int MaxUomLength = 50;

    /// <summary>The MOD-0022 module of the signable actions (pack §8.8; registration and subject provider in R4).</summary>
    public const string ESignModule = "recall";

    public const string CaseSubjectType = "recall.case";
    public const string SettingsSubjectType = "recall.settings-change";

    /// <summary>CT-R16: <c>recall.settings.update</c> is signable with <c>defaultRequired=false</c> (reason mandatory).</summary>
    public const string SettingsUpdateAction = "recall.settings.update";

    public const string DefaultMeaningCode = "APPROVE";
    public const string DefaultVisualMode = "savedAccepted";

    // AUD-001 (pack §8.12) — the R1 events.
    public const string AuditSettingsUpdated = "recall.settings.updated";
    public const string AuditCaseOpened = "recall.case.opened";
    public const string AuditCaseCancelled = "recall.case.cancelled";
    public const string AuditScopeItemsAdded = "recall.scope.items-added";
    public const string AuditScopeMarked = "recall.scope.marked";
    public const string AuditScopeSubmitted = "recall.scope.submitted";
    public const string AuditScopeReturned = "recall.scope.returned";
    public const string AuditActionPrepared = "recall.case.action-prepared";
    public const string AuditScopeApproved = "recall.scope.approved";
    public const string AuditSegregationConfirmed = "recall.segregation.confirmed";
    public const string AuditCustomerNotified = "recall.customer.notified";
    public const string AuditConsumptionDeclared = "recall.consumption.declared";
    public const string AuditEffectivenessStarted = "recall.effectiveness.started";
    public const string AuditEffectivenessCompleted = "recall.effectiveness.completed";
    public const string AuditCaseClosed = "recall.case.closed";

    // R2 (pack §8.12)
    public const string AuditTraceRun = "recall.trace.run";
    public const string AuditScopeDiffed = "recall.scope.diffed";
    public const string AuditOutOfScopeFinding = "recall.scope.out-of-scope-finding";

    public const string SubjectTrace = "recall.trace";

    public const string SubjectSettings = "recall.settings";
    public const string SubjectCase = "recall.case";

    /// <summary>The wire type of a real case row (drill runs join the list in R5 as <c>Drill</c>).</summary>
    public const string CaseTypeReal = "Real";
}
