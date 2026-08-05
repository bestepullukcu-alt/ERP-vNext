using Diten.FpaService.Domain.Modules.Budgeting.Common;
using Diten.FpaService.Domain.Modules.Budgeting.Financial;

namespace Diten.FpaService.Domain.Modules.Budgeting.Versions;

public static class BudgetCertificationFactory
{
    public static BudgetVersion Certify(Budget budget,BudgetVersionDraft draft,BudgetFiscalCalendar calendar,Guid versionId,long number,Guid certifier,DateTimeOffset now,long expectedDraftFence=0,ApprovalCertificationPrerequisite? approval=null)
    {
        approval=(approval??ApprovalCertificationPrerequisite.NotRequired).EnsureSatisfied();
        if(budget.IsDeleted||draft.IsAbandoned||draft.Version!=expectedDraftFence||budget.Id!=draft.BudgetId||budget.TenantId!=draft.TenantId||number<=0||certifier==draft.CreatedBy||certifier==draft.UpdatedBy||draft.Lines.Count==0||approval.Required&&approval.Reference is null)throw new BudgetingDomainException("budgeting.certification.invalid");
        if(calendar.Reference!=draft.CalendarReference||!calendar.Periods.Any(x=>draft.EffectiveFrom>=x.LocalStartDate&&draft.EffectiveFrom<x.LocalEndDateExclusive))throw new BudgetingDomainException("budgeting.calendar.certification-mismatch");
        var refs=calendar.Periods.Select(x=>x.PeriodReference).ToHashSet(StringComparer.Ordinal);if(draft.Lines.Any(x=>!refs.Contains(x.PeriodReference)))throw new BudgetingDomainException("budgeting.line.orphan-period");
        var periods=calendar.Periods.Select(x=>new BudgetPeriod(Guid.NewGuid(),versionId,x.PeriodReference,x.Order,x.LocalStartDate,x.LocalEndDateExclusive)).ToArray();
        var lines=draft.Lines.Select(x=>new BudgetLine(versionId,x.PeriodReference,x.LineKey,x.LineType,x.Amount)).ToArray();
        _=BudgetAmount.CheckedAggregate(lines.Select(x=>x.Amount));
        var bytes=BudgetContentCanonicalizer.Write(budget.Id,number,draft.RevisionLabel,draft.CurrencyReference,draft.CalendarReference,draft.UnitSemantics,draft.FundingBaselineType,draft.EffectiveFrom,draft.Notes,periods,lines,approval.Reference);
        return new(versionId,draft.TenantId,budget.Id,number,draft.RevisionLabel,draft.CalendarReference,draft.CurrencyReference,draft.UnitSemantics,draft.FundingBaselineType,draft.EffectiveFrom,draft.Notes,approval.Reference,now,certifier,periods,lines,BudgetContentCanonicalizer.Hash(bytes));
    }
}

public sealed class ApprovalOutcomeReference
{
    private ApprovalOutcomeReference(string contractName,string contractVersion,Guid approvalOutcomeId){ContractName=contractName;ContractVersion=contractVersion;ApprovalOutcomeId=approvalOutcomeId;}
    public string ContractName{get;}public string ContractVersion{get;}public Guid ApprovalOutcomeId{get;}
    public static ApprovalOutcomeReference FromRatifiedTuple(string contractName,string contractVersion,Guid approvalOutcomeId)=>throw new BudgetingDomainException("budgeting.approval-reference.not-ratified");
}
public sealed record ApprovalCertificationPrerequisite(bool Required,ApprovalOutcomeReference? Reference)
{
    public static ApprovalCertificationPrerequisite NotRequired{get;}=new(false,null);
    public static ApprovalCertificationPrerequisite RequiredButUnratified{get;}=new(true,null);
    public ApprovalCertificationPrerequisite EnsureSatisfied(){if(Required&&Reference is null||!Required&&Reference is not null)throw new BudgetingDomainException("budgeting.approval-prerequisite.invalid");return this;}
}
