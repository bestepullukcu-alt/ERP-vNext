using System.Text;
using Diten.FpaService.Application.Modules.ScenarioPlanning;
using Diten.FpaService.Domain.Modules.ScenarioPlanning;
using Diten.FpaService.Domain.Modules.ScenarioPlanning.Common;

namespace Diten.FpaService.UnitTests.Modules.ScenarioPlanning;

public sealed class ScenarioPlanningCoreTests
{
    private static readonly Guid Tenant=Guid.Parse("13800000-0000-0000-0000-000000000004");
    [Fact] public void Lifecycle_is_active_to_archived_only()
    {var s=new Scenario(Guid.NewGuid(),Tenant,"BASE","Base",null,DateTimeOffset.UtcNow);Assert.Equal(ScenarioLifecycle.Active,s.Lifecycle);s.Archive(DateTimeOffset.UtcNow);Assert.Equal(ScenarioLifecycle.Archived,s.Lifecycle);Assert.Throws<ScenarioPlanningDomainException>(()=>s.Archive(DateTimeOffset.UtcNow));}
    [Fact] public void One_open_draft_guard_is_tenant_and_scenario_scoped()
    {var f=new OpenDraftFence();var id=Guid.NewGuid();f.Acquire(Tenant,id);Assert.Throws<ScenarioPlanningDomainException>(()=>f.Acquire(Tenant,id));f.Release(Tenant,id);f.Acquire(Tenant,id);}
    [Fact] public void Published_version_is_an_immutable_copy()
    {var d=Draft();d.ReplaceContent(0,[A("GrowthRate","0.05")],[],DateTimeOffset.UtcNow);var bytes=ScenarioContentCanonicalizer.Canonicalize("Base",new(2026,1,1),new(2026,12,31),null,d.Assumptions,[]);var v=new ScenarioVersion(Guid.NewGuid(),Tenant,d.ScenarioId,1,d,ScenarioContentCanonicalizer.Sha256(bytes),DateTimeOffset.UtcNow,Guid.NewGuid());d.ReplaceContent(1,[A("GrowthRate","0.06")],[],DateTimeOffset.UtcNow);Assert.Equal("0.05",v.Assumptions.Single().Value);Assert.Equal(1,v.ScenarioVersionNumber);Assert.DoesNotContain(typeof(ScenarioVersion).GetProperties(),x=>x.SetMethod?.IsPublic==true);}
    [Fact] public void Minimal_canonical_vector_is_exact_309_bytes_and_hash()
    {var actual=ScenarioContentCanonicalizer.Canonicalize("Base",new(2026,1,1),new(2026,12,31),null,[A("GrowthRate","0.05")],[]);Assert.Equal(309,actual.Length);Assert.Equal("38ff8c101b72104847a1a34765c5fd23e1ea423f8a05b6488c2e2ece288a7788",ScenarioContentCanonicalizer.Sha256(actual));Assert.DoesNotContain((byte)0xef,actual.Take(1));Assert.NotEqual((byte)'\n',actual[^1]);}
    [Fact] public void Full_canonical_vector_is_exact_623_bytes_and_hash()
    {var refs=new[]{new BudgetVersionReferenceV1("fpa.budget-version-reference","1.0",Guid.Parse("11111111-1111-1111-1111-111111111111"),Guid.Parse("22222222-2222-2222-2222-222222222222"),3)};var assumptions=new[]{A("Headcount","42",ScenarioAssumptionValueType.Integer,ScenarioAssumptionUnitKind.Units),A("Cost","1250000.0000",ScenarioAssumptionValueType.Decimal,ScenarioAssumptionUnitKind.Millions)};var actual=ScenarioContentCanonicalizer.Canonicalize("Plan",new(2026,1,1),new(2026,12,31),"Plan",assumptions,refs);Assert.Equal(623,actual.Length);Assert.Equal("b7dbd424e46da5f3d1d12739dff22be2c55f606a7a959dce3e0ad0f530bcd80b",ScenarioContentCanonicalizer.Sha256(actual));Assert.Contains("\"Cost\"",Encoding.UTF8.GetString(actual));}
    [Theory][InlineData("1e2")][InlineData("1.00000")][InlineData("12345678901234567890123456789")]
    public void Decimal_mutants_fail(string value)=>Assert.Throws<ScenarioPlanningDomainException>(()=>A("K",value).Validate());
    [Fact] public void Assumption_is_exact_five_field_and_currency_condition_is_closed()
    {Assert.Equal(5,typeof(ScenarioAssumptionV1).GetProperties().Length);Assert.Throws<ScenarioPlanningDomainException>(()=>new ScenarioAssumptionV1("K",ScenarioAssumptionValueType.Decimal,"1",ScenarioAssumptionUnitKind.Currency,null).Validate());}
    [Fact] public void Comparator_is_deterministic_and_tie_breaks_by_id()
    {var b=R("00000000-0000-0000-0000-000000000010");var c1=R("00000000-0000-0000-0000-000000000012");var c2=R("00000000-0000-0000-0000-000000000011");var run=new ComparatorRunDefinition(Guid.NewGuid(),[new(b,new Dictionary<string,decimal>{{"A",10}}),new(c1,new Dictionary<string,decimal>{{"A",12}}),new(c2,new Dictionary<string,decimal>{{"A",12}})],new Dictionary<string,string>());var result=BaselineDeltaV1.Execute(run);Assert.Equal(c2,result[0].Reference);Assert.All(result,x=>Assert.Equal(2,x.Score));Assert.Throws<ScenarioPlanningDomainException>(()=>new ComparatorRunDefinition(Guid.NewGuid(),[new(b,new Dictionary<string,decimal>())],new Dictionary<string,string>()));Assert.Throws<ScenarioPlanningDomainException>(()=>new ComparatorRunDefinition(Guid.NewGuid(),[new(b,new Dictionary<string,decimal>()),new(c1,new Dictionary<string,decimal>())],new Dictionary<string,string>{{"x","y"}}));}
    [Fact] public void Selection_is_append_only_and_fenced()
    {var f=new ScenarioSelectionFence();var ctx=new InvestmentCaseContextV1("ppm.investment-case-context","1.0",Guid.NewGuid());var first=f.Replace(0,Guid.NewGuid(),Tenant,ctx,R(Guid.NewGuid().ToString()),DateTimeOffset.UtcNow);Assert.Throws<ScenarioPlanningDomainException>(()=>f.Replace(0,Guid.NewGuid(),Tenant,ctx,R(Guid.NewGuid().ToString()),DateTimeOffset.UtcNow));f.Replace(1,Guid.NewGuid(),Tenant,ctx,R(Guid.NewGuid().ToString()),DateTimeOffset.UtcNow);Assert.Contains(f.Dispositions,x=>x.SelectionOccurrenceId==first.SelectionOccurrenceId&&x.Disposition=="Replaced");Assert.DoesNotContain(typeof(ScenarioSelectionOccurrence).GetProperties(),x=>x.Name=="IsCurrent");}
    [Fact] public void Idempotency_and_unknown_commit_contracts_are_closed()
    {var receipt=new IdempotencyReceipt(new(Tenant,"actor","op","key"),"aaa","Committed","locator");Assert.Equal(200,IdempotencyContract.ValidateReplay(receipt,"aaa"));Assert.Equal(409,IdempotencyContract.ValidateReplay(receipt,"bbb"));Assert.Equal(ReceiptReconciliation.Indeterminate503,IdempotencyContract.Reconcile("Pending"));}
    private static ScenarioVersionDraft Draft()=>new(Guid.NewGuid(),Tenant,Guid.NewGuid(),"Base",new(2026,1,1),new(2026,12,31),null,null,DateTimeOffset.UtcNow);
    private static ScenarioAssumptionV1 A(string key,string value,ScenarioAssumptionValueType type=ScenarioAssumptionValueType.Decimal,ScenarioAssumptionUnitKind unit=ScenarioAssumptionUnitKind.None)=>new(key,type,value,unit,null);
    private static ScenarioVersionReferenceV1 R(string id)=>new("fpa.scenario-planning-reference","1.0",Guid.NewGuid(),Guid.Parse(id),1);
}
