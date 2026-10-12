using System.Text.Json;
using Diten.SupplyChainService.Application.Features.Claims;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimContractTests
{
 private const string Base="{\"shipmentId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"reasonCode\":\"\",\"claimedAmount\":\"0001.123456789012345678901234567890\",\"currency\":\"ZZZ\"}";
 [Fact] public void Create_OpaqueEvidencePreservesEmptyDuplicatesOrder()
 {
  using var d=JsonDocument.Parse(Base[..^1]+",\"evidenceReferenceIds\":[\"\",\"e1\",\"e1\"]}");
  Assert.True(ClaimWire.CreateValid(d.RootElement));Assert.Equal(new[]{"","e1","e1"},ClaimWire.Plan(d.RootElement).EvidenceReferenceIds);
 }
 [Theory][InlineData(",\"evidenceReferenceIds\":null")][InlineData(",\"tenantId\":\"x\"")][InlineData(",\"reasonCode\":\"duplicate\"")]
 public void Create_SchemaInvalidMembers_Reject(string extra) {using var d=JsonDocument.Parse(Base[..^1]+extra+"}");Assert.False(ClaimWire.CreateValid(d.RootElement));}
 [Fact] public void Create_NumberTokenCannotReplaceDecimalString() {using var d=JsonDocument.Parse(Base.Replace("\"0001.123456789012345678901234567890\"","1"));Assert.False(ClaimWire.CreateValid(d.RootElement));}
 [Theory][InlineData("2026-09-20T12:34:56.1234567890123456789+03:00",true)][InlineData("2026-02-30T12:00:00Z",false)][InlineData("2026-09-20T12:00:00",false)]
 public void Instant_ValidatesWithoutPrecisionNarrowing(string instant,bool valid)=>Assert.Equal(valid,ClaimWire.Instant(instant));
}
