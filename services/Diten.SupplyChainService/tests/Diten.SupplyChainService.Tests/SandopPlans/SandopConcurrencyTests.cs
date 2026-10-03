using Xunit;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Tests.SandopPlans;
public sealed class SandopConcurrencyTests
{[Fact]public async Task Concurrent_different_keys_cannot_record_same_role_twice()
{var(h,db,s,f)=await SandopTestHost.Open();var create=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),SandopTestHost.Create());Assert.Equal(201,create.Status);var id=SandopTestHost.Id(create,"sandopPlanId");var capture=await SandopTestHost.Mutate(h,s,f,SandopAction.Capture,id,Guid.NewGuid().ToString(),SandopTestHost.Capture());Assert.Equal(201,capture.Status);var snap=SandopTestHost.Id(capture,"snapshotId");var body=SandopTestHost.J(System.Text.Json.JsonSerializer.Serialize(new { snapshotId=snap, role="Operations", decision="Approved" }));
 var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>SandopTestHost.Mutate(h,s,f,SandopAction.SignOff,id,Guid.NewGuid().ToString(),body)));
 Assert.Single(results,x=>x.Status==201);Assert.Single(results,x=>x.Status==409&&x.Code=="SIGN_OFF_ALREADY_RECORDED");
}
}
