using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Domain.Features.Claims;
using Xunit;
using System.Text.Json;
using Diten.SupplyChainService.Application.Features.Claims.Commands;
using Diten.SupplyChainService.Application.Features.Claims.Handlers.CommandHandlers;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimPermissionTests
{
 [Theory][InlineData(ClaimStatus.Open,"create")][InlineData(ClaimStatus.Investigating,"investigate")]
 [InlineData(ClaimStatus.Withdrawn,"investigate")][InlineData(ClaimStatus.Approved,"decide")][InlineData(ClaimStatus.Rejected,"decide")]
 [InlineData(ClaimStatus.Settled,"settle")][InlineData(ClaimStatus.Closed,"decide")]
 public void TargetPermission_MatchesApprovedActionPolicy(ClaimStatus target,string suffix)=>Assert.Equal("supplychain.claims."+suffix,ClaimWire.Permission(target));
 [Theory][InlineData(ClaimStatus.Withdrawn,"create",403)][InlineData(ClaimStatus.Withdrawn,"investigate",200)]
 [InlineData(ClaimStatus.Closed,"settle",403)][InlineData(ClaimStatus.Closed,"decide",200)]
 [InlineData(ClaimStatus.Approved,"create",403)][InlineData(ClaimStatus.Approved,"decide",200)]
 public async Task Handler_TargetGrantDenied_DoesNotReachReceiptOrAggregate(ClaimStatus target,string grant,int expected)
 {
  var repo=new CountingRepository();var context=new ClaimRequestContext {Scope=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()),Permissions=["supplychain.claims."+grant],CorrelationId=Guid.NewGuid(),IdempotencyKey="k"};
  using var doc=JsonDocument.Parse("{\"targetStatus\":\""+target+"\",\"occurredAt\":\"2026-09-20T00:00:00Z\"}");
  var result=await new TransitionClaimHandler(repo,context,new ForbiddenReader()).Handle(new TransitionClaimCommand(Guid.NewGuid(),doc.RootElement),default);
  Assert.Equal(expected,result.StatusCode);Assert.Equal(expected==403?0:1,repo.Calls);
 }
 private sealed class ForbiddenReader:IClaimReferenceReader
 {public Task<ClaimReferenceSnapshot> ObserveAsync(Claim c,CancellationToken ct)=>throw new InvalidOperationException("transition must not read source");}
 private sealed class CountingRepository:IClaimRepository
 {
  public int Calls;
  public Task<IReadOnlyList<Claim>> QueryAsync(ClaimScope s,ClaimStatus? status,Guid? shipment,CancellationToken ct)=>throw new NotSupportedException();
  public Task<ClaimMutationResult> MutateAsync(ClaimScope s,Guid? id,string key,string fp,Guid root,Claim? create,ClaimStatus? target,string? at,string? amount,string? resolution,string? note,Func<Claim,CancellationToken,Task<ClaimReferenceSnapshot>> observe,CancellationToken ct)
  {Calls++;return Task.FromResult(new ClaimMutationResult(id!.Value,"CLM-test",Guid.NewGuid(),target!.ToString()!,null,false,200));}
 }
}
