using Xunit;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.Returns;
[Collection("Returns Mongo")]
public sealed class ReturnLifecycleTests
{
 private static readonly HashSet<(string,string)> Edges=[("Requested","Authorized"),("Requested","Rejected"),("Authorized","InTransit"),("Authorized","Cancelled"),("InTransit","Received"),("Received","Dispositioned"),("Dispositioned","Closed")];
 [Fact] public void Lifecycle_All64Pairs_MatchesSevenCanonicalArrows()
 {var states=Enum.GetValues<ReturnStatus>();Assert.Equal(8,states.Length);int count=0;foreach(var a in states)foreach(var b in states){var expected=Edges.Contains((a.ToString(),b.ToString()));Assert.Equal(expected,ReturnLifecycle.Allows(a,b));if(expected)count++;}Assert.Equal(7,count);Assert.False(ReturnLifecycle.Allows(ReturnStatus.InTransit,ReturnStatus.Cancelled));}
 [Fact] public void Entitlement_OnlyRejectedCancelled_Release()
 {foreach(var state in Enum.GetValues<ReturnStatus>()){Assert.Equal(state is ReturnStatus.Rejected or ReturnStatus.Cancelled,ReturnLifecycle.Releases(state));Assert.Equal(state is not(ReturnStatus.Rejected or ReturnStatus.Cancelled),ReturnLifecycle.Counts(state));}}
 [Fact] public async Task Lifecycle_ActualRepository_All64PairsRejectWithoutWrites()
 {
  foreach(var source in Enum.GetValues<ReturnStatus>())foreach(var target in Enum.GetValues<ReturnStatus>())
  {
   var f=await ReturnTestFixture.New();var created=await f.Create();Assert.Equal(201,created.StatusCode);
   await f.Db.GetCollection<BsonDocument>("returns").UpdateOneAsync(f.Filter,new BsonDocument("$set",new BsonDocument("Status",source.ToString())));
   var before=await f.Counts();var result=await f.Transition(created.ReturnId,target,"arrow",disposition:target==ReturnStatus.Dispositioned?" ":null);
   if(Edges.Contains((source.ToString(),target.ToString()))){Assert.Equal(200,result.StatusCode);Assert.Equal(target.ToString(),result.Status);}
   else{Assert.Equal(422,result.StatusCode);Assert.Equal("INVALID_RETURN_TRANSITION",result.ErrorCode);Assert.Equal(before,await f.Counts());}
  }
 }
}
