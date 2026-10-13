using Diten.SupplyChainService.Domain.Features.Claims;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimOutboxTests
{
 [Fact] public async Task Mutations_PersistPendingOutboxAndOriginalTransitionTime()
 {
  var f=await ClaimDbFixture.Create();var c=await f.CreateClaim("create");Assert.Equal(201,c.StatusCode);
  Assert.Equal(200,(await f.Transition(c.ClaimId,ClaimStatus.Investigating,"investigate")).StatusCode);
  var docs=await f.Db.GetCollection<BsonDocument>("claims_outbox").Find(f.Filter()).ToListAsync();
  Assert.Equal(2,docs.Count);
  foreach(var d in docs)
  {
   Assert.Equal("Pending",d["Status"].AsString);var envelope=d["Envelope"].AsBsonDocument;
   Assert.Equal(f.Root.ToString(),envelope["correlationId"].AsString);Assert.True(envelope["causationId"].IsBsonNull);
   Assert.Equal("v1",envelope["contractVersion"].AsString);Assert.Equal("Claim",envelope["aggregateType"].AsString);
   Assert.Equal(c.ClaimId.ToString(),envelope["aggregateId"].AsString);
  }
  Assert.Equal(2,docs.Select(d=>d["EventId"].AsString).Distinct().Count());
  Assert.Contains(docs,d=>d["Envelope"]["eventType"].AsString=="ClaimOpened");
  Assert.Contains(docs,d=>d["Envelope"]["eventType"].AsString=="ClaimInvestigating");
  Assert.Contains(docs,d=>d.ToJson().Contains("2026-09-20T12:34:56.123456789+03:00"));
  var before=await f.Counts();var replay=await f.Transition(c.ClaimId,ClaimStatus.Investigating,"investigate");Assert.True(replay.IdempotentReplay);Assert.Equal(before,await f.Counts());
 }
}
