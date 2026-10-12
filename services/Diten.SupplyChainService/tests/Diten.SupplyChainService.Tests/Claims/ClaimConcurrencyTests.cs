using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Claims;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimConcurrencyTests
{
 [Fact] public async Task DefiniteNumberCollisionAfterSoftDelete_DoesNotRenumberOrPartiallyWrite()
 {
  var id=Guid.NewGuid();var f=await ClaimDbFixture.Create(new RecordingClaimProbe {FixedId=id});
  var first=await f.CreateClaim("number-first");Assert.Equal(201,first.StatusCode);Assert.Equal("CLM-"+id.ToString("N"),first.ClaimNumber);
  await f.Db.GetCollection<BsonDocument>("claims").UpdateOneAsync(f.Filter(),Builders<BsonDocument>.Update.Set("IsDeleted",true));
  var collision=await f.CreateClaim("number-second");Assert.Equal(503,collision.StatusCode);Assert.Equal("CLAIM_STORAGE_UNAVAILABLE",collision.ErrorCode);
  Assert.Equal(new long[]{1,1,1,1},await f.Counts());
 }

 [Fact] public async Task Create_TwentySameKeys_OneFourCollectionCommit()
 {
  var f=await ClaimDbFixture.Create();var results=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>f.CreateClaim("same")));
  Assert.All(results,r=>Assert.Equal(201,r.StatusCode));Assert.Single(results,r=>!r.IdempotentReplay);
  Assert.Single(results.Select(r=>r.ClaimId).Distinct());Assert.Equal(new long[]{1,1,1,1},await f.Counts());
 }
 [Fact] public async Task Create_TwentyDifferentKeysSameShipment_AllIndependentClaims()
 {
  var f=await ClaimDbFixture.Create();var results=await Task.WhenAll(Enumerable.Range(0,20).Select(i=>f.CreateClaim("distinct-"+i)));
  Assert.All(results,r=>Assert.Equal(201,r.StatusCode));Assert.Equal(20,results.Select(r=>r.ClaimId).Distinct().Count());Assert.Equal(new long[]{20,20,20,20},await f.Counts());
 }
 [Fact] public async Task Transition_ConcurrentApproveReject_OneWinnerAndLifecycleLoser()
 {
  var f=await ClaimDbFixture.Create();var c=await f.CreateClaim("create");Assert.Equal(201,c.StatusCode);
  Assert.Equal(200,(await f.Transition(c.ClaimId,ClaimStatus.Investigating,"investigate")).StatusCode);
  var results=await Task.WhenAll(f.Transition(c.ClaimId,ClaimStatus.Approved,"approve","0"),f.Transition(c.ClaimId,ClaimStatus.Rejected,"reject"));
  Assert.Single(results,r=>r.StatusCode==200);Assert.Single(results,r=>r.StatusCode==422&&r.ErrorCode=="INVALID_CLAIM_TRANSITION");
  Assert.Equal(new long[]{1,3,3,3},await f.Counts());
 }
}
