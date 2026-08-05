using System.Reflection;
using Diten.FpaService.Domain.Modules.Budgeting.Versions;
using Diten.FpaService.Persistence.Modules.Budgeting.Ownership;
using Diten.FpaService.Persistence.Modules.Budgeting.Contracts;

namespace Diten.FpaService.ArchitectureTests.Modules.Budgeting;

public sealed class BudgetingArchitectureTests
{
    [Fact] public void Certified_version_is_immutable_by_public_surface()
    {
        Assert.Empty(typeof(BudgetVersion).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(typeof(BudgetVersion).GetProperties(), property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    [Fact] public void Persistence_manifest_is_single_exact_and_tenant_first()
    {
        Assert.Equal(14, BudgetingPersistenceOwnershipManifest.Collections.Count);
        Assert.Equal(14, BudgetingPersistenceOwnershipManifest.Collections.Select(x => x.Collection).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(26, BudgetingPersistenceOwnershipManifest.Indexes.Count);
        Assert.Equal(26, BudgetingPersistenceOwnershipManifest.Indexes.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(BudgetingPersistenceOwnershipManifest.Indexes, index => Assert.Equal(("TenantId",1), index.Keys[0]));
        Assert.DoesNotContain(BudgetingPersistenceOwnershipManifest.Collections, x => x.Collection.Contains("scenario", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public void Exact_manifest_and_transaction_structures_are_closed_snapshots()
    {
        var collectionSnapshot=string.Join('\n',BudgetingPersistenceOwnershipManifest.Collections.Select(x=>$"{x.Entry}|{x.Collection}|{x.Repository}|{x.Writer}"));
        Assert.Equal("Budgets|budgeting_budgets|IBudgetRepository|BudgetRepository\nFiscalCalendars|budgeting_fiscal_calendars|IBudgetFiscalCalendarRepository|BudgetFiscalCalendarRepository\nBudgetVersionDrafts|budgeting_budget_version_drafts|IBudgetVersionDraftRepository|BudgetVersionDraftRepository\nBudgetVersions|budgeting_budget_versions|IBudgetVersionRepository|BudgetVersionRepository\nBudgetVersionDispositions|budgeting_budget_version_dispositions|IBudgetVersionRepository|BudgetVersionRepository\nBudgetPeriods|budgeting_budget_periods|IBudgetPeriodRepository|BudgetPeriodRepository\nBudgetLines|budgeting_budget_lines|IBudgetLineRepository|BudgetLineRepository\nFundingBaselineSelectionOccurrences|budgeting_funding_baseline_selection_occurrences|IFundingBaselineSelectionRepository|IFundingBaselineSelectionRepository\nFundingBaselineSelectionDispositions|budgeting_funding_baseline_selection_dispositions|IFundingBaselineSelectionRepository|IFundingBaselineSelectionRepository\nFundingBaselineSelectionFences|budgeting_funding_baseline_selection_fences|IFundingBaselineSelectionRepository|IFundingBaselineSelectionRepository\nIdempotencyReceipts|budgeting_idempotency_receipts|IBudgetingIdempotencyReceiptStore|IBudgetingIdempotencyReceiptStore\nAuditIntents|budgeting_audit_intents|IBudgetingAuditIntentWriter|IBudgetingAuditIntentWriter\nEventOutbox|budgeting_event_outbox|IBudgetingOutboxWriter|IBudgetingOutboxWriter\nMigrationQuarantine|budgeting_migration_quarantine|IBudgetingMigrationQuarantineWriter|IBudgetingMigrationQuarantineWriter",collectionSnapshot);
        var transactionSnapshot=string.Join('\n',BudgetingTransactionCompositionManifest.Transactions.Select(x=>$"{x.Name}|{string.Join(',',x.BusinessParticipants)}|{string.Join(',',x.AtomicParticipants)}"));
        Assert.Equal("BudgetIdentityMutationV1|budgeting_budgets|budgeting_budgets,budgeting_idempotency_receipts,budgeting_audit_intents,budgeting_event_outbox\nBudgetFiscalCalendarMutationV1|budgeting_fiscal_calendars|budgeting_fiscal_calendars,budgeting_idempotency_receipts,budgeting_audit_intents,budgeting_event_outbox\nBudgetVersionDraftMutationV1|budgeting_budget_version_drafts|budgeting_budget_version_drafts,budgeting_idempotency_receipts,budgeting_audit_intents,budgeting_event_outbox\nBudgetVersionCertificationV1|budgeting_budget_version_drafts,budgeting_budget_versions,budgeting_budget_periods,budgeting_budget_lines|budgeting_budget_version_drafts,budgeting_budget_versions,budgeting_budget_periods,budgeting_budget_lines,budgeting_idempotency_receipts,budgeting_audit_intents,budgeting_event_outbox\nFundingBaselineSelectionReplaceV1|budgeting_funding_baseline_selection_fences,budgeting_funding_baseline_selection_dispositions,budgeting_funding_baseline_selection_occurrences|budgeting_funding_baseline_selection_fences,budgeting_funding_baseline_selection_dispositions,budgeting_funding_baseline_selection_occurrences,budgeting_idempotency_receipts,budgeting_audit_intents,budgeting_event_outbox\nFundingBaselineSelectionCloseV1|budgeting_funding_baseline_selection_fences,budgeting_funding_baseline_selection_dispositions|budgeting_funding_baseline_selection_fences,budgeting_funding_baseline_selection_dispositions,budgeting_idempotency_receipts,budgeting_audit_intents,budgeting_event_outbox\nBudgetVersionRetireV1|budgeting_budget_version_dispositions|budgeting_budget_version_dispositions,budgeting_idempotency_receipts,budgeting_audit_intents,budgeting_event_outbox",transactionSnapshot);
        var indexSnapshot=string.Join('\n',BudgetingPersistenceOwnershipManifest.Indexes.Select(x=>$"{x.Name}|{x.Collection}|{string.Join(',',x.Keys.Select(k=>$"{k.Field}:{k.Direction}"))}|{x.Unique}|{x.ActiveOnly}"));
        Assert.Equal("2baa45d8efb395af9e379e00daf87bbdbdee271e8e0ac5d72e29beb41b45d14c",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(indexSnapshot))).ToLowerInvariant());
    }
}
