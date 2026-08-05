using System.Text;
using Diten.FpaService.Domain.Modules.Budgeting.Common;
using Diten.FpaService.Domain.Modules.Budgeting.Financial;

namespace Diten.FpaService.Domain.Modules.Budgeting.Versions;

public sealed class Budget : EntityBase
{
    public Budget(Guid id,Guid tenantId,string code,string name,string? description,CurrencyReferenceV1 currency,BudgetFiscalCalendarReferenceV1 calendar,UnitSemantics units,Guid actor,DateTimeOffset now)
    {Id=BudgetingGuard.Required(id,"budgeting.budget.id-required");TenantId=BudgetingGuard.Required(tenantId,"budgeting.tenant.required");Code=BudgetingGuard.Text(code,64,"budgeting.budget.code-invalid");Name=BudgetingGuard.Text(name,200,"budgeting.budget.name-invalid");Description=description?.Trim().Normalize(NormalizationForm.FormC);DefaultCurrencyReference=currency;DefaultFiscalCalendarReference=calendar;UnitSemantics=units;CreatedBy=BudgetingGuard.Required(actor,"budgeting.actor.required");CreatedAt=now;}
    public string Code{get;private set;}public string Name{get;private set;}public string? Description{get;private set;}public CurrencyReferenceV1 DefaultCurrencyReference{get;private set;}public BudgetFiscalCalendarReferenceV1 DefaultFiscalCalendarReference{get;private set;}public UnitSemantics UnitSemantics{get;private set;}public Guid CreatedBy{get;}public Guid? UpdatedBy{get;private set;}
    public void Archive(Guid actor,DateTimeOffset now){IsDeleted=true;DeletedAt=now;UpdatedAt=now;UpdatedBy=actor;Version++;}
}

public sealed record BudgetVersionDraftLine(string PeriodReference,string LineKey,LineType LineType,BudgetAmount Amount)
{
    public BudgetVersionDraftLine Validate(){var key=LineKey.Trim().Normalize(NormalizationForm.FormC);if(string.IsNullOrWhiteSpace(PeriodReference)||key.Length is 0 or>128)throw new BudgetingDomainException("budgeting.line.identity-invalid");if((LineType==LineType.Base&&Amount.Value<0)||(LineType==LineType.Credit&&Amount.Value>0))throw new BudgetingDomainException("budgeting.line.sign-invalid");return this with{LineKey=key};}
}

public sealed class BudgetVersionDraft : EntityBase
{
    private readonly List<BudgetVersionDraftLine> _lines=[];
    public BudgetVersionDraft(Guid id,Guid tenantId,Guid budgetId,string label,BudgetFiscalCalendarReferenceV1 calendar,CurrencyReferenceV1 currency,UnitSemantics units,FundingBaselineType baseline,DateOnly effectiveFrom,string? notes,Guid actor,DateTimeOffset now)
    {Id=BudgetingGuard.Required(id,"budgeting.draft.id-required");TenantId=BudgetingGuard.Required(tenantId,"budgeting.tenant.required");BudgetId=BudgetingGuard.Required(budgetId,"budgeting.budget.id-required");RevisionLabel=BudgetingGuard.Text(label,120,"budgeting.revision.invalid");CalendarReference=calendar;CurrencyReference=currency;UnitSemantics=units;FundingBaselineType=baseline;EffectiveFrom=effectiveFrom;Notes=notes?.Trim().Normalize(NormalizationForm.FormC);CreatedBy=actor;UpdatedBy=actor;CreatedAt=now;}
    public Guid BudgetId{get;}public string RevisionLabel{get;private set;}public BudgetFiscalCalendarReferenceV1 CalendarReference{get;private set;}public CurrencyReferenceV1 CurrencyReference{get;private set;}public UnitSemantics UnitSemantics{get;private set;}public FundingBaselineType FundingBaselineType{get;private set;}public DateOnly EffectiveFrom{get;private set;}public string? Notes{get;private set;}public Guid CreatedBy{get;}public Guid UpdatedBy{get;private set;}public bool IsAbandoned=>IsDeleted;public IReadOnlyList<BudgetVersionDraftLine> Lines=>_lines.AsReadOnly();
    public void UpdateHeader(long expectedFence,string revisionLabel,BudgetFiscalCalendarReferenceV1 calendar,CurrencyReferenceV1 currency,UnitSemantics units,FundingBaselineType baseline,DateOnly effectiveFrom,string? notes,Guid actor,DateTimeOffset now)
    {if(expectedFence!=Version||IsAbandoned)throw new BudgetingDomainException("budgeting.draft.stale-fence");RevisionLabel=BudgetingGuard.Text(revisionLabel,120,"budgeting.revision.invalid");CalendarReference=calendar;CurrencyReference=currency;UnitSemantics=units;FundingBaselineType=baseline;EffectiveFrom=effectiveFrom;Notes=notes is null?null:BudgetingGuard.Text(notes,2000,"budgeting.notes.invalid");UpdatedBy=BudgetingGuard.Required(actor,"budgeting.actor.required");UpdatedAt=now;Version++;}
    public void AddLine(BudgetVersionDraftLine input){var line=input.Validate();if(_lines.Any(x=>x.PeriodReference==line.PeriodReference&&x.LineKey==line.LineKey&&x.LineType==line.LineType))throw new BudgetingDomainException("budgeting.line.duplicate");_lines.Add(line);Version++;}
    public void Abandon(Guid actor,DateTimeOffset now){IsDeleted=true;DeletedAt=now;UpdatedBy=actor;UpdatedAt=now;Version++;}
}

public sealed class BudgetPeriod { public BudgetPeriod(Guid id,Guid versionId,string reference,int order,DateOnly start,DateOnly end){BudgetPeriodId=id;BudgetVersionId=versionId;PeriodReference=reference;Order=order;LocalStartDate=start;LocalEndDateExclusive=end;} public Guid BudgetPeriodId{get;}public Guid BudgetVersionId{get;}public string PeriodReference{get;}public int Order{get;}public DateOnly LocalStartDate{get;}public DateOnly LocalEndDateExclusive{get;} }
public sealed class BudgetLine { public BudgetLine(Guid versionId,string reference,string key,LineType type,BudgetAmount amount){BudgetVersionId=versionId;PeriodReference=reference;LineKey=key;LineType=type;Amount=amount;} public Guid BudgetVersionId{get;}public string PeriodReference{get;}public string LineKey{get;}public LineType LineType{get;}public BudgetAmount Amount{get;} }

public sealed class BudgetVersion : EntityBase
{
    internal BudgetVersion(Guid id,Guid tenantId,Guid budgetId,long number,string label,BudgetFiscalCalendarReferenceV1 calendar,CurrencyReferenceV1 currency,UnitSemantics units,FundingBaselineType baseline,DateOnly effectiveFrom,string? notes,ApprovalOutcomeReference? approval,DateTimeOffset certifiedAt,Guid certifiedBy,IReadOnlyList<BudgetPeriod> periods,IReadOnlyList<BudgetLine> lines,string hash)
    {Id=id;TenantId=tenantId;BudgetId=budgetId;BudgetVersionNumber=number;RevisionLabel=label;CalendarReference=calendar;CurrencyReference=currency;UnitSemantics=units;RoundingPolicy=RoundingPolicyV1.Instance;FundingBaselineType=baseline;EffectiveFrom=effectiveFrom;Notes=notes;ApprovalOutcomeReference=approval;CertifiedAtUtc=certifiedAt;CertifiedByActorId=certifiedBy;Periods=Array.AsReadOnly(periods.ToArray());Lines=Array.AsReadOnly(lines.ToArray());ContentHash=hash;CreatedAt=certifiedAt;}
    public Guid BudgetId{get;}public long BudgetVersionNumber{get;}public string RevisionLabel{get;}public BudgetFiscalCalendarReferenceV1 CalendarReference{get;}public CurrencyReferenceV1 CurrencyReference{get;}public UnitSemantics UnitSemantics{get;}public RoundingPolicyV1 RoundingPolicy{get;}public FundingBaselineType FundingBaselineType{get;}public DateOnly EffectiveFrom{get;}public string? Notes{get;}public ApprovalOutcomeReference? ApprovalOutcomeReference{get;}public DateTimeOffset CertifiedAtUtc{get;}public Guid CertifiedByActorId{get;}public string ContentHashAlgorithm=>"SHA-256";public string ContentCanonicalizationVersion=>"fpa.budget-content-canonical-json.v1";public string ContentHash{get;}public IReadOnlyList<BudgetPeriod> Periods{get;}public IReadOnlyList<BudgetLine> Lines{get;}
}
