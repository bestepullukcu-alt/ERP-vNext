using Diten.SupplyChainService.Domain.Features.Claims;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimAmountTests
{
    [Fact] public async Task Approval_ZeroRetainedThroughSettlementAndClosure_ExactBsonStrings()
    {
        var f=await ClaimDbFixture.Create();var c=await f.CreateClaim("create",amount:"000250.0000000000000000000000000000000000000000");Assert.Equal(201,c.StatusCode);
        Assert.Equal(200,(await f.Transition(c.ClaimId,ClaimStatus.Investigating,"investigate")).StatusCode);
        Assert.Equal(200,(await f.Transition(c.ClaimId,ClaimStatus.Approved,"approve","-0")).StatusCode);
        Assert.Equal(200,(await f.Transition(c.ClaimId,ClaimStatus.Settled,"settle")).StatusCode);
        var closed=await f.Transition(c.ClaimId,ClaimStatus.Closed,"close");Assert.Equal(200,closed.StatusCode);Assert.Equal("-0",closed.ApprovedAmount);
        var row=Assert.Single(await f.Repository.QueryAsync(f.Scope,null,null,default));Assert.Equal("-0",row.ApprovedAmount);Assert.Equal("000250.0000000000000000000000000000000000000000",row.ClaimedAmount);
    }
    [Theory]
    [InlineData("1e2")][InlineData("+1")][InlineData("1.")][InlineData(".1")]
    [InlineData(" 1")][InlineData("1 ")][InlineData("١")][InlineData("")][InlineData("--1")]
    public void Parse_WhenNotAsciiDecimal_Rejects(string text) => Assert.False(ExactClaimAmount.TryParse(text, out _));
    [Theory]
    [InlineData("0",false,false)][InlineData("-0",false,false)][InlineData("-0.000",false,false)]
    [InlineData("000250.00",true,false)][InlineData("-0.01",false,true)]
    [InlineData("0.0000000000000000000000000000000000000001",true,false)]
    public void Parse_WhenValid_PreservesLexemeAndNumericSign(string text,bool positive,bool negative)
    {
        var amount=ExactClaimAmount.Parse(text);
        Assert.Equal(text,amount.Original); Assert.Equal(positive,amount.IsPositive); Assert.Equal(negative,amount.IsNegative);
    }
    [Theory]
    [InlineData("000250.00","250",0)][InlineData("-0","0",0)]
    [InlineData("1.0000000000000000000000000000000000001","1",1)]
    [InlineData("-1.0000000000000000000000000000000000001","-1",-1)]
    [InlineData("99999999999999999999999999999999999999","100000000000000000000000000000000000000",-1)]
    public void Comparison_WhenBeyondDecimalPrecision_IsExact(string left,string right,int expected)
    {
        Assert.Equal(expected,Math.Sign(ExactClaimAmount.Parse(left).CompareTo(ExactClaimAmount.Parse(right))));
        Assert.Equal(-expected,Math.Sign(ExactClaimAmount.Parse(right).CompareTo(ExactClaimAmount.Parse(left))));
    }
}
