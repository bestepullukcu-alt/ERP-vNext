using Xunit;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.Returns;
[Collection("Returns Mongo")]
public sealed class ReturnConcurrencyTests
{
 [Theory][InlineData("6","6",1)][InlineData("4","6",2)]public async Task Create_Races_RespectsExactCap(string first,string second,int successes)
 {var f=await ReturnTestFixture.New();var result=await Task.WhenAll(f.Create(first,"one"),f.Create(second,"two"));Assert.Equal(successes,result.Count(x=>x.StatusCode==201));if(successes==1)Assert.Equal("RETURN_QUANTITY_EXCEEDED",result.Single(x=>x.StatusCode!=201).ErrorCode);Assert.Equal(successes==1?"6":"10",(await f.Entitlement())["UsedQuantity"].AsString);}
 [Fact] public async Task SameKey_TwentyConcurrentCalls_OneAggregateReceiptEvent()
 {var f=await ReturnTestFixture.New();var results=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>f.Create()));Assert.All(results,x=>Assert.Equal(201,x.StatusCode));Assert.Single(results.Select(x=>x.ReturnId).Distinct());Assert.Equal(new long[]{1,1,1,1,1},await f.Counts());}
 [Theory][InlineData(ReturnStatus.Rejected)][InlineData(ReturnStatus.Cancelled)]
 public async Task Release_ReplayAndDifferentKey_DebitsExactlyOnce(ReturnStatus target)
 {var f=await ReturnTestFixture.New();var created=await f.Create("10");if(target==ReturnStatus.Cancelled)Assert.Equal(200,(await f.Transition(created.ReturnId,ReturnStatus.Authorized,"authorize")).StatusCode);Assert.Equal(200,(await f.Transition(created.ReturnId,target,"release")).StatusCode);Assert.Equal("0",(await f.Entitlement())["UsedQuantity"].AsString);Assert.True((await f.Transition(created.ReturnId,target,"release")).IdempotentReplay);Assert.Equal(422,(await f.Transition(created.ReturnId,target,"another")).StatusCode);Assert.Equal("0",(await f.Entitlement())["UsedQuantity"].AsString);Assert.Equal(201,(await f.Create("10","replacement")).StatusCode);}
 [Fact] public async Task FirstSnapshot_NumericEquivalentAllowed_DriftConflictsAtomically()
 {var f=await ReturnTestFixture.New();Assert.Equal(201,(await f.Create()).StatusCode);var before=await f.Counts();var line=f.SourceLines[0];f.SourceLines[0]=line with{Quantity="010.00"};Assert.Equal(201,(await f.Create("1","equivalent")).StatusCode);before=await f.Counts();f.SourceLines[0]=line with{Quantity="11"};var fail=await f.Create("1","drift");Assert.Equal(409,fail.StatusCode);Assert.Equal("RETURN_SOURCE_CHANGED",fail.ErrorCode);Assert.Equal(before,await f.Counts());}
 [Theory][InlineData("uom")][InlineData("item")][InlineData("sku")]
 public async Task FirstSnapshot_IdentityOrOrdinalUomDrift_Returns409(string field)
 {var f=await ReturnTestFixture.New();Assert.Equal(201,(await f.Create()).StatusCode);var before=await f.Counts();var line=f.SourceLines[0];f.SourceLines[0]=field switch{"uom"=>line with{UomId="ea"},"item"=>line with{ItemId=Guid.NewGuid()},_=>line with{SkuId=Guid.NewGuid()}};var fail=await f.Create("1","changed");Assert.Equal("RETURN_SOURCE_CHANGED",fail.ErrorCode);Assert.Equal(before,await f.Counts());}
 [Fact] public async Task ClosedAndManualReceived_RetainEntitlementOpaqueReferenceAndPendingEvents()
 {var f=await ReturnTestFixture.New();var c=await f.Create("10");foreach(var target in new[]{ReturnStatus.Authorized,ReturnStatus.InTransit,ReturnStatus.Received,ReturnStatus.Dispositioned,ReturnStatus.Closed}){var r=await f.Transition(c.ReturnId,target,target.ToString(),inventory:target==ReturnStatus.Received?"opaque-no-lookup":null,disposition:target==ReturnStatus.Dispositioned?" ":null);Assert.Equal(200,r.StatusCode);}Assert.Equal("10",(await f.Entitlement())["UsedQuantity"].AsString);Assert.Equal(1,f.Observations);Assert.Equal("RETURN_QUANTITY_EXCEEDED",(await f.Create("1","more")).ErrorCode);var auditFilter=f.Filter;auditFilter.Add("ToStatus","Received");var row=await f.Db.GetCollection<BsonDocument>("returns_audit").Find(auditFilter).FirstAsync();Assert.Equal("opaque-no-lookup",row["InventoryTransactionReferenceId"].AsString);Assert.Equal("manual-assertion",row["EvidenceType"].AsString);Assert.Equal("2026-09-20T09:00:00.123456789Z",row["OccurredAt"].AsString);var events=await f.Db.GetCollection<BsonDocument>("returns_outbox").Find(f.Filter).ToListAsync();Assert.Equal(6,events.Count);Assert.All(events,e=>Assert.Equal("Pending",e["Status"].AsString));}
 [Fact]public async Task RmaNumber_KnownCollision_MaxThreeAttemptsAndAtomicRecovery()
 {var f=await ReturnTestFixture.New();var first=await f.Create();var before=await f.Counts();for(var i=0;i<3;i++)f.Probe.Ids.Enqueue(first.ReturnId);var fail=await f.Create("1","collision");Assert.Equal(503,fail.StatusCode);Assert.Empty(f.Probe.Ids);Assert.Equal(before,await f.Counts());f.Probe.Ids.Enqueue(first.ReturnId);var fresh=Guid.NewGuid();f.Probe.Ids.Enqueue(fresh);var ok=await f.Create("1","collision");Assert.Equal(201,ok.StatusCode);Assert.Equal(fresh,ok.ReturnId);Assert.Equal("RMA-"+fresh.ToString("N").ToUpperInvariant(),ok.RmaNumber);Assert.Equal(new long[]{1,2,2,2,2},await f.Counts());}
 [Fact]public async Task Entitlement_DefaultCaseInsensitiveCollection_UsesExplicitOrdinalOperations()
 {var f=await ReturnTestFixture.New("diten_returns_collation_tests",true);var item=Guid.NewGuid();var sku=Guid.NewGuid();f.SourceLines.Clear();f.SourceLines.Add(new("A","10","EA",item,sku));f.SourceLines.Add(new("a","10","EA",item,sku));Assert.Equal(201,(await f.Create("6","upper",lines:[new("A","6","EA")])).StatusCode);Assert.Equal(201,(await f.Create("6","lower",lines:[new("a","6","EA")])).StatusCode);var rows=await f.Db.GetCollection<BsonDocument>("return_entitlements").Find(f.Filter).ToListAsync();Assert.Equal(2,rows.Count);Assert.Equal(new[]{"A","a"},rows.Select(x=>x["LineNumber"].AsString).OrderBy(x=>x,StringComparer.Ordinal));Assert.All(rows,x=>Assert.Equal("6",x["UsedQuantity"].AsString));}

 /// <summary>
 /// Gate 18.0 row 5 for MOD-0186. Every race in this file until now was a CREATE race; Q446 measured that no
 /// concurrent conflicting TRANSITION existed in any record under docs/records, while acceptance row R07
 /// claimed one. This is that race, and it is a real conflict rather than two attempts at the same thing:
 /// ReturnLifecycle.cs:6 allows Authorized to go to InTransit OR Cancelled, so two actors each hold a legal
 /// move and only one can be the one that happened. They are two distinct actors sharing the tenant and legal
 /// entity, with distinct idempotency keys, so neither replay nor key reuse can be what settles it.
 ///
 /// The outcome must hold whichever move lands, and measurement showed that asserting "either winner is fine"
 /// is not enough on its own: across eleven runs of the first version of this test, the first-issued move won
 /// ELEVEN times out of eleven (all eleven stored rows were InTransit), so the Cancelled branch of the
 /// assertions had never executed. A race whose schedule never varies tests one schedule. So the ordering is
 /// a parameter here: each target gets a turn as the first-issued move, and both branches are exercised.
 ///
 /// What must hold either way is that exactly one move took effect and the observable state agrees with it
 /// end to end: the stored status, the audit row and the entitlement. Cancelled releases the entitlement and
 /// InTransit does not, so the entitlement is the sharpest check available here — if both moves had landed,
 /// or one had landed twice, the used quantity could not match the single winner.
 /// </summary>
 [Theory]
 [InlineData("InTransit","Cancelled")]
 [InlineData("Cancelled","InTransit")]
 public async Task Transition_Races_TwoActorsOneReturn_ExactlyOneMoveTakesEffect(string firstIssued,string secondIssued)
 {
  var f=await ReturnTestFixture.New();
  var created=await f.Create("10");
  Assert.Equal(200,(await f.Transition(created.ReturnId,ReturnStatus.Authorized,"authorize")).StatusCode);
  var armed=await f.Counts();

  var actorA=f.Scope with{ActorId=Guid.NewGuid()};
  var actorB=f.Scope with{ActorId=Guid.NewGuid()};
  Assert.NotEqual(actorA.ActorId,actorB.ActorId);
  Task<ReturnMutationResult> Move(ReturnScope actor,string target,string key)=>
   f.Repository.MutateAsync(actor,created.ReturnId,key,target,f.Root,null,Enum.Parse<ReturnStatus>(target),
    "2026-09-20T09:00:00.123456789Z",null,null,f.Observe,default);

  var results=await Task.WhenAll(Move(actorA,firstIssued,"actor-a-"+firstIssued),
                                 Move(actorB,secondIssued,"actor-b-"+secondIssued));

  var winners=results.Where(x=>x.StatusCode==200).ToArray();
  var losers=results.Where(x=>x.StatusCode!=200).ToArray();
  Assert.Single(winners);
  Assert.Single(losers);
  Assert.Equal(422,losers[0].StatusCode);
  Assert.Equal("INVALID_RETURN_TRANSITION",losers[0].ErrorCode);

  var won=winners[0].Status;
  Assert.Contains(won,new[]{firstIssued,secondIssued});

  // The stored row is the winner's move and nothing else.
  var row=await f.Db.GetCollection<BsonDocument>("returns").Find(f.Filter).SingleAsync();
  Assert.Equal(won,row["Status"].AsString);

  // Exactly one transition audit row for this move, written by the winning actor.
  var auditFilter=f.Filter;auditFilter.Add("ToStatus",won);
  var audit=await f.Db.GetCollection<BsonDocument>("returns_audit").Find(auditFilter).ToListAsync();
  Assert.Single(audit);
  var winningActor=won==firstIssued?actorA.ActorId:actorB.ActorId;
  Assert.Equal(winningActor.ToString(),audit[0]["ActorId"].AsString);

  // One move, one new audit row and one new event; the loser wrote nothing.
  var after=await f.Counts();
  Assert.Equal(armed[0],after[0]);
  Assert.Equal(armed[1],after[1]);
  Assert.Equal(armed[3]+1,after[3]);
  Assert.Equal(armed[4]+1,after[4]);

  // Cancelled releases the entitlement, InTransit keeps it. Either way the number agrees with the one winner.
  Assert.Equal(won=="Cancelled"?"0":"10",(await f.Entitlement())["UsedQuantity"].AsString);
 }

 /// <summary>
 /// The companion the race cannot provide. Across thirteen measured races InTransit won every single one,
 /// whichever move was issued first, so the branch where Cancelled is the move that happened has never been
 /// reached concurrently and CT did not establish why InTransit always wins (raised as Q488, not guessed at).
 /// A conflict proven in one direction only is half proven, so the symmetry is measured sequentially instead:
 /// whichever of the two legal moves out of Authorized is applied first, the other is refused afterwards with
 /// the same 422 INVALID_RETURN_TRANSITION, and the entitlement follows the move that happened. Sequential is
 /// the honest instrument here — it settles the state machine's symmetry and claims nothing about scheduling.
 /// </summary>
 [Theory]
 [InlineData("InTransit","Cancelled","10")]
 [InlineData("Cancelled","InTransit","0")]
 public async Task Transition_ConflictIsSymmetric_EitherTargetRefusesTheOther(string applied,string refused,string usedAfter)
 {
  var f=await ReturnTestFixture.New();
  var created=await f.Create("10");
  Assert.Equal(200,(await f.Transition(created.ReturnId,ReturnStatus.Authorized,"authorize")).StatusCode);
  var armed=await f.Counts();

  var landed=await f.Transition(created.ReturnId,Enum.Parse<ReturnStatus>(applied),"applied-"+applied);
  Assert.Equal(200,landed.StatusCode);
  Assert.Equal(applied,landed.Status);

  var rejected=await f.Transition(created.ReturnId,Enum.Parse<ReturnStatus>(refused),"refused-"+refused);
  Assert.Equal(422,rejected.StatusCode);
  Assert.Equal("INVALID_RETURN_TRANSITION",rejected.ErrorCode);

  // The refusal wrote nothing: one new audit row and one new event, from the move that landed.
  var after=await f.Counts();
  Assert.Equal(armed[3]+1,after[3]);
  Assert.Equal(armed[4]+1,after[4]);
  var row=await f.Db.GetCollection<BsonDocument>("returns").Find(f.Filter).SingleAsync();
  Assert.Equal(applied,row["Status"].AsString);
  Assert.Equal(usedAfter,(await f.Entitlement())["UsedQuantity"].AsString);
 }
}
