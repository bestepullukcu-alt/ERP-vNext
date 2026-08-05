namespace Diten.FpaService.Persistence.Modules.Budgeting.Contracts;

public sealed record TransactionComposition(string Name, IReadOnlyList<string> BusinessParticipants)
{
    public IReadOnlyList<string> AtomicParticipants => [.. BusinessParticipants, "budgeting_idempotency_receipts", "budgeting_audit_intents", "budgeting_event_outbox"];
}

public static class BudgetingTransactionCompositionManifest
{
    public static IReadOnlyList<TransactionComposition> Transactions { get; } =
    [
        new("BudgetIdentityMutationV1",["budgeting_budgets"]),
        new("BudgetFiscalCalendarMutationV1",["budgeting_fiscal_calendars"]),
        new("BudgetVersionDraftMutationV1",["budgeting_budget_version_drafts"]),
        new("BudgetVersionCertificationV1",["budgeting_budget_version_drafts","budgeting_budget_versions","budgeting_budget_periods","budgeting_budget_lines"]),
        new("FundingBaselineSelectionReplaceV1",["budgeting_funding_baseline_selection_fences","budgeting_funding_baseline_selection_dispositions","budgeting_funding_baseline_selection_occurrences"]),
        new("FundingBaselineSelectionCloseV1",["budgeting_funding_baseline_selection_fences","budgeting_funding_baseline_selection_dispositions"]),
        new("BudgetVersionRetireV1",["budgeting_budget_version_dispositions"])
    ];
}

public interface IBudgetingIdempotencyReceiptStore { }
public interface IBudgetingAuditIntentWriter { }
public interface IBudgetingOutboxWriter { }
public interface IBudgetingMigrationQuarantineWriter { }
