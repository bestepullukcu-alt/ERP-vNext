using Xunit;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.Returns;
public sealed class ReturnContractTests
{
 private static JsonElement Body(string text)=>JsonDocument.Parse(text).RootElement.Clone();
 [Theory][InlineData("1e2")][InlineData("+1")][InlineData(" 1")][InlineData("1.")][InlineData(".1")][InlineData("١")]
 public void Quantity_InvalidLexical_Rejects(string value)=>Assert.False(ReturnQuantity.TryParse(value,out _));
 [Theory][InlineData("1.0","1.00")][InlineData("-0","000.000")][InlineData("123456789012345678901234567890.123456789012345678901234567890","123456789012345678901234567890.12345678901234567890123456789")]
 public void Quantity_EquivalentNumericValues_Equal(string a,string b)=>Assert.Equal(ReturnQuantity.Parse(a),ReturnQuantity.Parse(b));
 [Fact] public void Quantity_Arithmetic_HasNoPrecisionLoss(){var tiny=ReturnQuantity.Parse("0.0000000000000000000000000000000000000001");Assert.True(tiny.CompareTo(ReturnQuantity.Zero)>0);Assert.Equal("1",(ReturnQuantity.Parse("1")+tiny-tiny).ToString());}
 [Fact] public void Fingerprint_NumericAndInstantEquivalence_ReplaysWhileArrayOrderConflicts()
 {
  var id=Guid.NewGuid();var a=Body($$"""{"shipmentId":"{{id}}","reasonCode":"","lines":[{"shipmentLineNumber":"1","quantity":"01.00","uomId":"EA"}]}""");
  var b=Body($$"""{"reasonCode":"","shipmentId":"{{id.ToString().ToUpperInvariant()}}","lines":[{"shipmentLineNumber":"1","quantity":"1","uomId":"EA"}],"evidenceReferenceIds":[]}""");
  Assert.Equal(ReturnRequestFingerprint.Create(a),ReturnRequestFingerprint.Create(b));
  var t1=Body("""{"targetStatus":"Received","occurredAt":"2026-09-20T12:00:00.123456789123400+03:00"}""");
  var t2=Body("""{"targetStatus":"Received","occurredAt":"2026-09-20T09:00:00.1234567891234Z","inventoryTransactionReferenceId":null,"dispositionCode":null}""");
  Assert.Equal(ReturnRequestFingerprint.Transition(t1),ReturnRequestFingerprint.Transition(t2));
  var t3=Body(t2.GetRawText().Replace("1234567891234","1234567891235"));Assert.NotEqual(ReturnRequestFingerprint.Transition(t1),ReturnRequestFingerprint.Transition(t3));
  var e1=Body(b.GetRawText().Replace("[]","""["a","b"]"""));var e2=Body(b.GetRawText().Replace("[]","""["b","a"]"""));Assert.NotEqual(ReturnRequestFingerprint.Create(e1),ReturnRequestFingerprint.Create(e2));
 }
 [Fact] public void Wire_BoundedShape_RejectsNullEvidenceAndExtrasAllowsOpaqueEmpty()
 {
  var text=$$"""{"shipmentId":"{{Guid.Empty}}","reasonCode":"","lines":[{"shipmentLineNumber":"","quantity":"1","uomId":""}],"evidenceReferenceIds":["","a","a"]}""";
  Assert.True(ReturnWire.CreateValid(Body(text)));Assert.False(ReturnWire.CreateValid(Body(text.Replace("""["","a","a"]""","null"))));Assert.False(ReturnWire.CreateValid(Body(text[..^1]+""","tenantId":"x"}""")));
  Assert.False(ReturnWire.TransitionValid(Body("""{"targetStatus":"Received","occurredAt":"bad"}""")));
 }
 [Fact] public void Instant_LeapSecond_OffsetEquivalentDistinctFromNextSecond()
 {Assert.Equal(ReturnInstant.Normalize("2016-12-31T23:59:60Z"),ReturnInstant.Normalize("2017-01-01T00:59:60+01:00"));Assert.NotEqual(ReturnInstant.Normalize("2016-12-31T23:59:60Z"),ReturnInstant.Normalize("2017-01-01T00:00:00Z"));Assert.Throws<FormatException>(()=>ReturnInstant.Normalize("2016-12-30T23:59:60Z"));Assert.Throws<FormatException>(()=>ReturnInstant.Normalize("2016-12-31T22:59:60Z"));}
 [Fact] public void Instant_GregorianYearZeroAndBoundaries_KeepEquivalentInstants()
 {Assert.True(ReturnWire.Instant("0000-02-29T00:00:00Z"));Assert.False(ReturnWire.Instant("0000-02-30T00:00:00Z"));Assert.Equal(ReturnInstant.Normalize("0001-01-01T00:00:00.123456789+01:00"),ReturnInstant.Normalize("0000-12-31T23:00:00.123456789Z"));foreach(var text in new[]{"0000-01-01T00:30:00+01:00","9999-12-31T23:30:00-01:00"}){var normalized=ReturnInstant.Normalize(text);Assert.True(ReturnWire.Instant(normalized));Assert.Equal(normalized,ReturnInstant.Normalize(normalized));}}
}
