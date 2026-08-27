using Diten.FpaService.Application.Modules.Budgeting.Contracts;
using Diten.FpaService.Application.Modules.Budgeting.GateI;
using Diten.FpaService.Domain.Modules.Budgeting.Selections;

namespace Diten.FpaService.UnitTests.Modules.Budgeting.GateI;
public sealed class BudgetVersionReferenceValidationProviderTests
{
    private static readonly Guid Tenant=Guid.NewGuid(), Budget=Guid.NewGuid(), Version=Guid.NewGuid(), Case=Guid.NewGuid();
    [Theory]
    [InlineData(BudgetVersionValidationMode.HistoricalResolve,false,false,200)]
    [InlineData(BudgetVersionValidationMode.NewReferenceEligibility,true,false,409)]
    [InlineData(BudgetVersionValidationMode.CurrentSelectionEligibility,false,false,409)]
    [InlineData(BudgetVersionValidationMode.CurrentSelectionEligibility,false,true,200)]
    public async Task Mode_matrix_is_exact(BudgetVersionValidationMode mode,bool retired,bool current,int expected)
    {
        var result=await Provider(new SnapshotReader(Snapshot(retired,current))).ValidateAsync(Request(mode),Context(),default);
        Assert.Equal(expected,result.StatusCode);
    }
    [Fact] public async Task Cross_tenant_is_404()
    {var result=await Provider(new SnapshotReader(Snapshot(tenant:Guid.NewGuid()))).ValidateAsync(Request(),Context(),default);Assert.Equal(404,result.StatusCode);}
    [Fact] public async Task Wrong_permission_is_403_without_read()
    {var reader=new SnapshotReader(Snapshot());var result=await Provider(reader).ValidateAsync(Request(),Context(permission:"alias"),default);Assert.Equal(403,result.StatusCode);Assert.Equal(0,reader.Reads);}
    [Fact] public async Task Default_is_disabled()
    {var result=await new BudgetVersionReferenceValidationProvider(new SnapshotReader(Snapshot()),BudgetProviderOptions.Disabled).ValidateAsync(Request(),Context(),default);Assert.Equal(503,result.StatusCode);}
    private static BudgetVersionReferenceValidationProvider Provider(SnapshotReader r)=>new(r,new(true));
    private static BudgetVersionValidationRequest Request(BudgetVersionValidationMode mode=BudgetVersionValidationMode.NewReferenceEligibility)=>new(new("fpa.budget-version-reference","1.0",Budget,Version,1),mode,new("ppm.investment-case-context","1.0",Case));
    private static BudgetGateITrustedContext Context(string permission=BudgetGateIProfile.Permission)=>new(true,Tenant,Guid.NewGuid(),BudgetGateIProfile.ClientId,BudgetGateIProfile.Audience,BudgetGateIProfile.Scope,BudgetGateIProfile.Operation,permission,new string('a',64));
    private static BudgetVersionSnapshot Snapshot(bool retired=false,bool current=true,Guid? tenant=null)=>new(true,tenant??Tenant,Budget,Version,1,true,retired,current);
    private sealed class SnapshotReader(BudgetVersionSnapshot value):IBudgetVersionSnapshotReader{public int Reads{get;private set;}public Task<BudgetVersionSnapshot?> ReadAsync(Guid tenantId,Guid budgetId,Guid versionId,Guid investmentCaseId,CancellationToken cancellationToken){Reads++;return Task.FromResult<BudgetVersionSnapshot?>(value);}}
}
