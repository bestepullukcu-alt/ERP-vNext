namespace Diten.FpaService.Persistence.Modules.Budgeting.Ownership;

public sealed record CollectionOwnership(string Entry, string Collection, string Repository, string Writer);
public sealed record IndexOwnership(string Name, string Collection, IReadOnlyList<(string Field, int Direction)> Keys, bool Unique = false, bool ActiveOnly = false);

public static class BudgetingPersistenceOwnershipManifest
{
    public static IReadOnlyList<CollectionOwnership> Collections { get; } =
    [
        new("Budgets","budgeting_budgets","IBudgetRepository","BudgetRepository"),
        new("FiscalCalendars","budgeting_fiscal_calendars","IBudgetFiscalCalendarRepository","BudgetFiscalCalendarRepository"),
        new("BudgetVersionDrafts","budgeting_budget_version_drafts","IBudgetVersionDraftRepository","BudgetVersionDraftRepository"),
        new("BudgetVersions","budgeting_budget_versions","IBudgetVersionRepository","BudgetVersionRepository"),
        new("BudgetVersionDispositions","budgeting_budget_version_dispositions","IBudgetVersionRepository","BudgetVersionRepository"),
        new("BudgetPeriods","budgeting_budget_periods","IBudgetPeriodRepository","BudgetPeriodRepository"),
        new("BudgetLines","budgeting_budget_lines","IBudgetLineRepository","BudgetLineRepository"),
        new("FundingBaselineSelectionOccurrences","budgeting_funding_baseline_selection_occurrences","IFundingBaselineSelectionRepository","IFundingBaselineSelectionRepository"),
        new("FundingBaselineSelectionDispositions","budgeting_funding_baseline_selection_dispositions","IFundingBaselineSelectionRepository","IFundingBaselineSelectionRepository"),
        new("FundingBaselineSelectionFences","budgeting_funding_baseline_selection_fences","IFundingBaselineSelectionRepository","IFundingBaselineSelectionRepository"),
        new("IdempotencyReceipts","budgeting_idempotency_receipts","IBudgetingIdempotencyReceiptStore","IBudgetingIdempotencyReceiptStore"),
        new("AuditIntents","budgeting_audit_intents","IBudgetingAuditIntentWriter","IBudgetingAuditIntentWriter"),
        new("EventOutbox","budgeting_event_outbox","IBudgetingOutboxWriter","IBudgetingOutboxWriter"),
        new("MigrationQuarantine","budgeting_migration_quarantine","IBudgetingMigrationQuarantineWriter","IBudgetingMigrationQuarantineWriter")
    ];

    private static (string,int) K(string field,int direction) => (field,direction);
    public static IReadOnlyList<IndexOwnership> Indexes { get; } =
    [
        new("ux_budgeting_budgets_tenant_code_active_v1","budgeting_budgets",[K("TenantId",1),K("Code",1)],true,true),
        new("ix_budgeting_budgets_tenant_list_v1","budgeting_budgets",[K("TenantId",1),K("IsDeleted",1),K("UpdatedAt",-1),K("Id",1)]),
        new("ux_budgeting_fiscal_calendars_tenant_id_v1","budgeting_fiscal_calendars",[K("TenantId",1),K("Id",1)],true),
        new("ix_budgeting_fiscal_calendars_tenant_list_v1","budgeting_fiscal_calendars",[K("TenantId",1),K("IsDeleted",1),K("UpdatedAt",-1),K("Id",1)]),
        new("ux_budgeting_budget_version_drafts_tenant_id_v1","budgeting_budget_version_drafts",[K("TenantId",1),K("Id",1)],true),
        new("ix_budgeting_budget_version_drafts_tenant_budget_v1","budgeting_budget_version_drafts",[K("TenantId",1),K("BudgetId",1),K("IsDeleted",1),K("UpdatedAt",-1),K("Id",1)]),
        new("ux_budgeting_budget_versions_tenant_budget_number_v1","budgeting_budget_versions",[K("TenantId",1),K("BudgetId",1),K("BudgetVersionNumber",1)],true),
        new("ux_budgeting_budget_versions_tenant_id_v1","budgeting_budget_versions",[K("TenantId",1),K("Id",1)],true),
        new("ux_budgeting_budget_version_dispositions_tenant_id_v1","budgeting_budget_version_dispositions",[K("TenantId",1),K("Id",1)],true),
        new("ix_budgeting_budget_version_dispositions_tenant_version_time_v1","budgeting_budget_version_dispositions",[K("TenantId",1),K("BudgetVersionId",1),K("DisposedAtUtc",-1),K("Id",1)]),
        new("ux_budgeting_budget_periods_tenant_version_reference_v1","budgeting_budget_periods",[K("TenantId",1),K("BudgetVersionId",1),K("PeriodReference",1)],true),
        new("ux_budgeting_budget_periods_tenant_version_order_v1","budgeting_budget_periods",[K("TenantId",1),K("BudgetVersionId",1),K("Order",1)],true),
        new("ux_budgeting_budget_lines_tenant_version_period_key_type_v1","budgeting_budget_lines",[K("TenantId",1),K("BudgetVersionId",1),K("PeriodReference",1),K("LineKey",1),K("LineType",1)],true),
        new("ix_budgeting_budget_lines_tenant_version_order_v1","budgeting_budget_lines",[K("TenantId",1),K("BudgetVersionId",1),K("PeriodReference",1),K("LineKey",1),K("LineType",1),K("Id",1)]),
        new("ux_budgeting_selection_occurrences_tenant_id_v1","budgeting_funding_baseline_selection_occurrences",[K("TenantId",1),K("Id",1)],true),
        new("ix_budgeting_selection_occurrences_tenant_context_time_v1","budgeting_funding_baseline_selection_occurrences",[K("TenantId",1),K("FundingContext.ContractName",1),K("FundingContext.ContractVersion",1),K("FundingContext.InvestmentCaseId",1),K("EffectiveFromUtc",-1),K("Id",1)]),
        new("ux_budgeting_selection_dispositions_tenant_occurrence_v1","budgeting_funding_baseline_selection_dispositions",[K("TenantId",1),K("OccurrenceId",1)],true),
        new("ix_budgeting_selection_dispositions_tenant_context_time_v1","budgeting_funding_baseline_selection_dispositions",[K("TenantId",1),K("FundingContext.ContractName",1),K("FundingContext.ContractVersion",1),K("FundingContext.InvestmentCaseId",1),K("DisposedAtUtc",-1),K("Id",1)]),
        new("ux_budgeting_selection_fences_tenant_context_v1","budgeting_funding_baseline_selection_fences",[K("TenantId",1),K("FundingContext.ContractName",1),K("FundingContext.ContractVersion",1),K("FundingContext.InvestmentCaseId",1)],true),
        new("ux_budgeting_idempotency_tenant_actor_operation_key_v1","budgeting_idempotency_receipts",[K("TenantId",1),K("ActorId",1),K("Operation",1),K("CanonicalKey",1)],true),
        new("ix_budgeting_idempotency_tenant_created_v1","budgeting_idempotency_receipts",[K("TenantId",1),K("CreatedAt",-1),K("Id",1)]),
        new("ux_budgeting_audit_intents_tenant_id_v1","budgeting_audit_intents",[K("TenantId",1),K("Id",1)],true),
        new("ix_budgeting_audit_intents_tenant_delivery_v1","budgeting_audit_intents",[K("TenantId",1),K("DeliveryState",1),K("CreatedAt",1),K("Id",1)]),
        new("ux_budgeting_event_outbox_tenant_id_v1","budgeting_event_outbox",[K("TenantId",1),K("Id",1)],true),
        new("ix_budgeting_event_outbox_tenant_delivery_v1","budgeting_event_outbox",[K("TenantId",1),K("DeliveryState",1),K("NextAttemptAtUtc",1),K("CreatedAt",1),K("Id",1)]),
        new("ux_budgeting_migration_quarantine_tenant_source_v1","budgeting_migration_quarantine",[K("TenantId",1),K("SourceSystem",1),K("SourceCollection",1),K("SourceDocumentId",1),K("MappingVersion",1)],true)
    ];
}
