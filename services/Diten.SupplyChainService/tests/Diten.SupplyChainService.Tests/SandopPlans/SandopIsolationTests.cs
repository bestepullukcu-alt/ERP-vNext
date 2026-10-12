using Xunit;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Tests.SandopPlans;
public sealed class SandopIsolationTests
{[Fact]public async Task Tenant_and_legal_entity_cross_scope_reads_are_404()
{var(h,db,s,f)=await SandopTestHost.Open();var created=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),SandopTestHost.Create());Assert.Equal(201,created.Status);var id=SandopTestHost.Id(created,"sandopPlanId");
 Assert.Equal(404,(await h.ReadAsync(s with{TenantId=Guid.NewGuid()},SandopAction.Get,id,CancellationToken.None)).Status);
 Assert.Equal(404,(await h.ReadAsync(s with{LegalEntityId=Guid.NewGuid()},SandopAction.Get,id,CancellationToken.None)).Status);
}

 [Fact]public async Task Cross_scope_mutation_cannot_disclose_receipt_or_plan()
 {var(h,db,s,f)=await SandopTestHost.Open();var created=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),SandopTestHost.Create());Assert.Equal(201,created.Status);var id=SandopTestHost.Id(created,"sandopPlanId");var key=Guid.NewGuid().ToString();
 var foreign=s with{LegalEntityId=Guid.NewGuid()};var result=await SandopTestHost.Mutate(h,foreign,f,SandopAction.Capture,id,key,SandopTestHost.Capture());Assert.Equal(404,result.Status);Assert.Equal("UNKNOWN_SANDOP_PLAN",result.Code);
 }

}
