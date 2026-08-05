using Diten.FpaService.Application.Modules.Budgeting.Contracts;
using Diten.FpaService.Domain.Modules.Budgeting.Common;
using Diten.FpaService.Domain.Modules.Budgeting.Financial;
using Diten.FpaService.Domain.Modules.Budgeting.Versions;
using Diten.FpaService.Persistence.Modules.Budgeting.Contracts;

namespace Diten.FpaService.UnitTests.Modules.Budgeting;

public sealed class BudgetingInvariantTests
{
    [Fact] public void Amount_rejects_scale_and_bound_mutations()
    {
        Assert.Throws<BudgetingDomainException>(() => new BudgetAmount(0.00001m));
        Assert.Throws<BudgetingDomainException>(() => new BudgetAmount(BudgetAmount.LineAbsoluteLimit + 1m));
        Assert.Equal("1.2344", new BudgetAmount(BudgetAmount.NormalizeDerived(1.23445m)).Canonical);
    }

    [Theory]
    [InlineData("")][InlineData(" key")][InlineData("key ")][InlineData("ü")][InlineData("a/b")]
    public void Idempotency_key_rejects_noncanonical_values(string key) => Assert.False(BudgetingIdempotency.IsValidKey(key));

    [Fact] public void Idempotency_and_unknown_commit_never_reexecute_body()
    {
        var hash = BudgetingIdempotency.PayloadHash("{}"u8);
        var replay = BudgetingIdempotency.Decide(hash, new ReceiptState.Found(hash, "stable"));
        Assert.Equal((200, false, "stable"), (replay.StatusCode, replay.ExecuteBody, replay.StableResult));
        Assert.Equal(409, BudgetingIdempotency.Decide(hash, new ReceiptState.Found(new string('0',64), "x")).StatusCode);
        Assert.Equal(503, BudgetingIdempotency.Decide(hash, new ReceiptState.Absent()).StatusCode);
        Assert.Equal(UnknownCommitAction.RetryCommitOnSameSession, UnknownCommitPolicy.Decide(true,hash));
        Assert.Equal(UnknownCommitAction.ReturnStoredResult, UnknownCommitPolicy.Decide(false,hash,new ReceiptState.Found(hash,"stable")));
        Assert.Equal(UnknownCommitAction.ReturnConflict, UnknownCommitPolicy.Decide(false,hash,new ReceiptState.Found(new string('0',64),"other")));
        Assert.Equal(UnknownCommitAction.ReturnUnavailable, UnknownCommitPolicy.Decide(false,hash,new ReceiptState.Indeterminate()));
    }

    [Fact] public void Every_transaction_atomically_includes_receipt_audit_and_outbox()
    {
        Assert.Equal(7, BudgetingTransactionCompositionManifest.Transactions.Count);
        Assert.All(BudgetingTransactionCompositionManifest.Transactions, transaction =>
        {
            Assert.Contains("budgeting_idempotency_receipts", transaction.AtomicParticipants);
            Assert.Contains("budgeting_audit_intents", transaction.AtomicParticipants);
            Assert.Contains("budgeting_event_outbox", transaction.AtomicParticipants);
        });
    }

    [Fact] public void Draft_exact_seven_headers_update_only_on_current_fence()
    {
        var actor=Guid.NewGuid();var calendar=new BudgetFiscalCalendarReferenceV1("fpa.budget-fiscal-calendar-reference","1.0",Guid.NewGuid());var currency=new CurrencyReferenceV1("fpa.currency-reference","1.0","USD");
        var draft=new BudgetVersionDraft(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"v1",calendar,currency,UnitSemantics.Units,FundingBaselineType.Original,new DateOnly(2027,1,1),null,actor,DateTimeOffset.UtcNow);
        draft.UpdateHeader(0,"v2",calendar,currency,UnitSemantics.Thousands,FundingBaselineType.Reforecast,new DateOnly(2027,2,1),"note",Guid.NewGuid(),DateTimeOffset.UtcNow);
        Assert.Equal(("v2",UnitSemantics.Thousands,FundingBaselineType.Reforecast,new DateOnly(2027,2,1),"note",1L),(draft.RevisionLabel,draft.UnitSemantics,draft.FundingBaselineType,draft.EffectiveFrom,draft.Notes,draft.Version));
        Assert.Throws<BudgetingDomainException>(()=>draft.UpdateHeader(0,"stale",calendar,currency,UnitSemantics.Units,FundingBaselineType.Original,new DateOnly(2027,1,1),null,actor,DateTimeOffset.UtcNow));
    }

    [Fact] public void Aggregate_bound_overflow_and_to_even_are_fail_closed()
    {
        Assert.Equal(1.2344m,BudgetAmount.NormalizeDerived(1.23445m));
        Assert.Throws<BudgetingDomainException>(()=>BudgetAmount.CheckedAggregate(Enumerable.Repeat(new BudgetAmount(BudgetAmount.LineAbsoluteLimit),1001)));
        Assert.Throws<BudgetingDomainException>(()=>new BudgetAmount(1.00001m));
    }

    [Fact] public void Certified_children_and_selection_history_have_no_mutators()
    {
        foreach(var type in new[]{typeof(BudgetPeriod),typeof(BudgetLine),typeof(Diten.FpaService.Domain.Modules.Budgeting.Selections.FundingBaselineSelectionOccurrence),typeof(Diten.FpaService.Domain.Modules.Budgeting.Selections.SelectionDisposition)})
            Assert.All(type.GetProperties(),p=>Assert.False(p.SetMethod?.IsPublic??false));
    }

    [Fact] public void Conditional_approval_neither_invents_nor_omits_evidence()
    {
        Assert.Null(ApprovalCertificationPrerequisite.NotRequired.EnsureSatisfied().Reference);
        Assert.Throws<BudgetingDomainException>(()=>new ApprovalCertificationPrerequisite(true,null).EnsureSatisfied());
        Assert.Throws<BudgetingDomainException>(()=>ApprovalCertificationPrerequisite.RequiredButUnratified.EnsureSatisfied());
        Assert.Throws<BudgetingDomainException>(()=>ApprovalOutcomeReference.FromRatifiedTuple("unratified","1.0",Guid.NewGuid()));
    }
    [Fact] public void Fiscal_calendar_period_collection_cannot_be_cast_back_to_mutable_array()
    {
        var reference=new BudgetFiscalCalendarReferenceV1("fpa.budget-fiscal-calendar-reference","1.0",Guid.NewGuid());var start=new DateOnly(2027,1,1);var periods=Enumerable.Range(1,12).Select(i=>new BudgetFiscalCalendarPeriodDefinition($"FY27-P{i:00}",i,start.AddMonths(i-1),start.AddMonths(i))).ToArray();var calendar=new BudgetFiscalCalendar(reference,"Europe/Istanbul",1,periods);
        Assert.False(calendar.Periods is BudgetFiscalCalendarPeriodDefinition[]);periods[0]=periods[1];Assert.Equal("FY27-P01",calendar.Periods[0].PeriodReference);
    }
}
