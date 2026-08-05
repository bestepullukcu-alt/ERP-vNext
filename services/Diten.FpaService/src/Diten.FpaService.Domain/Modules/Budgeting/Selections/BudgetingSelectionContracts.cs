using Diten.FpaService.Domain.Modules.Budgeting.Common;
namespace Diten.FpaService.Domain.Modules.Budgeting.Selections;

public sealed record BudgetVersionReferenceV1
{
    public const string ExpectedContractName="fpa.budget-version-reference";public const string ExpectedContractVersion="1.0";
    public BudgetVersionReferenceV1(string contractName,string contractVersion,Guid budgetId,Guid versionId,long number){if(contractName!=ExpectedContractName||contractVersion!=ExpectedContractVersion||budgetId==Guid.Empty||versionId==Guid.Empty||number<=0)throw new BudgetingDomainException("budgeting.reference.invalid");ContractName=contractName;ContractVersion=contractVersion;BudgetId=budgetId;BudgetVersionId=versionId;BudgetVersionNumber=number;}
    public string ContractName{get;}public string ContractVersion{get;}public Guid BudgetId{get;}public Guid BudgetVersionId{get;}public long BudgetVersionNumber{get;}
}
public sealed record InvestmentCaseContextV1
{
    public const string ExpectedContractName="ppm.investment-case-context";public const string ExpectedContractVersion="1.0";
    public InvestmentCaseContextV1(string name,string version,Guid id){if(name!=ExpectedContractName||version!=ExpectedContractVersion||id==Guid.Empty)throw new BudgetingDomainException("budgeting.funding-context.invalid");ContractName=name;ContractVersion=version;InvestmentCaseId=id;}
    public string ContractName{get;}public string ContractVersion{get;}public Guid InvestmentCaseId{get;}
}
public sealed class FundingBaselineSelectionOccurrence { public FundingBaselineSelectionOccurrence(Guid id,Guid tenantId,InvestmentCaseContextV1 context,BudgetVersionReferenceV1 reference,DateTimeOffset effective,Guid actor,string reason,string provenance,Guid correlation,string key,long fence){Id=id;TenantId=tenantId;FundingContext=context;SelectedBudgetVersionReference=reference;EffectiveFromUtc=effective;SelectedByActorId=actor;ReasonCode=reason;ProvenanceReference=provenance;CorrelationId=correlation;IdempotencyKey=key;SelectionFence=fence;} public Guid Id{get;}public Guid TenantId{get;}public InvestmentCaseContextV1 FundingContext{get;}public BudgetVersionReferenceV1 SelectedBudgetVersionReference{get;}public DateTimeOffset EffectiveFromUtc{get;}public Guid SelectedByActorId{get;}public string ReasonCode{get;}public string ProvenanceReference{get;}public Guid CorrelationId{get;}public string IdempotencyKey{get;}public long SelectionFence{get;} }
public sealed class SelectionDisposition { public SelectionDisposition(Guid occurrenceId,DateTimeOffset at,Guid actor,string reason,string provenance,Guid correlation,string key,long fence,Guid? successor){OccurrenceId=occurrenceId;DisposedAtUtc=at;ActorId=actor;ReasonCode=reason;ProvenanceReference=provenance;CorrelationId=correlation;IdempotencyKey=key;SelectionFence=fence;SuccessorOccurrenceId=successor;} public Guid OccurrenceId{get;}public DateTimeOffset DisposedAtUtc{get;}public Guid ActorId{get;}public string ReasonCode{get;}public string ProvenanceReference{get;}public Guid CorrelationId{get;}public string IdempotencyKey{get;}public long SelectionFence{get;}public Guid? SuccessorOccurrenceId{get;} }
public static class FundingBaselineSelectionHistory
{
    public static (SelectionDisposition,FundingBaselineSelectionOccurrence) Replace(FundingBaselineSelectionOccurrence current,FundingBaselineSelectionOccurrence successor,Guid actor,DateTimeOffset now)
    {if(current.TenantId!=successor.TenantId||current.FundingContext!=successor.FundingContext||successor.SelectionFence!=current.SelectionFence+1)throw new BudgetingDomainException("budgeting.selection.conflict");return(new(current.Id,now,actor,successor.ReasonCode,successor.ProvenanceReference,successor.CorrelationId,successor.IdempotencyKey,current.SelectionFence,successor.Id),successor);}
}
