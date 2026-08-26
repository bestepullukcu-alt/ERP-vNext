using Diten.PpmService.Domain.GateI.FundingScenario;
using Xunit;

namespace Diten.PpmService.Tests.GateI.FundingScenario;

public sealed class FundingScenarioReferenceContractTests
{
    private static readonly InvestmentCaseContextV1 Context=new(InvestmentCaseContextV1.ExpectedContractName,"1.0",Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly BudgetVersionReferenceV1 Budget=new(BudgetVersionReferenceV1.ExpectedContractName,"1.0",Guid.Parse("22222222-2222-2222-2222-222222222222"),Guid.Parse("33333333-3333-3333-3333-333333333333"),7);
    private static readonly ScenarioVersionReferenceV1 Scenario=new(ScenarioVersionReferenceV1.ExpectedContractName,"1.0",Guid.Parse("44444444-4444-4444-4444-444444444444"),Guid.Parse("55555555-5555-5555-5555-555555555555"),3);
    private static readonly ComparatorOutputReferenceV1 Comparator=new(ComparatorOutputReferenceV1.ExpectedContractName,"1.0",Guid.Parse("66666666-6666-6666-6666-666666666666"),Guid.Parse("77777777-7777-7777-7777-777777777777"),1);

    [Fact]
    public void Exact_producer_and_ppm_wrappers_round_trip_without_copying_payload()
    {
        RoundTrip(Budget.ToExactJson(),BudgetVersionReferenceV1.ParseExact);
        RoundTrip(Scenario.ToExactJson(),ScenarioVersionReferenceV1.ParseExact);
        RoundTrip(Comparator.ToExactJson(),ComparatorOutputReferenceV1.ParseExact);
        RoundTrip(new SelectedBudgetVersionReferenceV1(SelectedBudgetVersionReferenceV1.ExpectedContractName,"1.0",Context,Budget).ToExactJson(),SelectedBudgetVersionReferenceV1.ParseExact);
        RoundTrip(new InvestmentCaseScenarioVersionReferenceV1(InvestmentCaseScenarioVersionReferenceV1.ExpectedContractName,"1.0",Context,Scenario).ToExactJson(),InvestmentCaseScenarioVersionReferenceV1.ParseExact);
        RoundTrip(new InvestmentCaseComparatorOutputReferenceV1(InvestmentCaseComparatorOutputReferenceV1.ExpectedContractName,"1.0",Context,Comparator).ToExactJson(),InvestmentCaseComparatorOutputReferenceV1.ParseExact);
        RoundTrip(new SelectedScenarioReferenceV1(SelectedScenarioReferenceV1.ExpectedContractName,"1.0",Context,Scenario).ToExactJson(),SelectedScenarioReferenceV1.ParseExact);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"ContractName\":\"fpa.budget-version-reference\",\"ContractVersion\":\"1.0\",\"BudgetId\":\"22222222-2222-2222-2222-222222222222\",\"BudgetVersionId\":\"33333333-3333-3333-3333-333333333333\",\"BudgetVersionNumber\":7,\"Amount\":10}")]
    [InlineData("{\"contractName\":\"fpa.budget-version-reference\",\"ContractVersion\":\"1.0\",\"BudgetId\":\"22222222-2222-2222-2222-222222222222\",\"BudgetVersionId\":\"33333333-3333-3333-3333-333333333333\",\"BudgetVersionNumber\":7}")]
    [InlineData("{\"ContractVersion\":\"1.0\",\"ContractName\":\"fpa.budget-version-reference\",\"BudgetId\":\"22222222-2222-2222-2222-222222222222\",\"BudgetVersionId\":\"33333333-3333-3333-3333-333333333333\",\"BudgetVersionNumber\":7}")]
    [InlineData("{\"ContractName\":\"fpa.budget-version-reference\",\"ContractName\":\"fpa.budget-version-reference\",\"ContractVersion\":\"1.0\",\"BudgetId\":\"22222222-2222-2222-2222-222222222222\",\"BudgetVersionId\":\"33333333-3333-3333-3333-333333333333\",\"BudgetVersionNumber\":7}")]
    public void Budget_parser_rejects_missing_extra_case_order_and_duplicate_fields(string json)=>Assert.ThrowsAny<Exception>(()=>BudgetVersionReferenceV1.ParseExact(json));

    [Fact]
    public void Scenario_wrappers_reject_cross_pair_and_payload_copy()
    {
        var selected=new SelectedScenarioReferenceV1(SelectedScenarioReferenceV1.ExpectedContractName,"1.0",Context,Scenario).ToExactJson();
        Assert.ThrowsAny<Exception>(()=>InvestmentCaseScenarioVersionReferenceV1.ParseExact(selected));
        Assert.ThrowsAny<Exception>(()=>SelectedScenarioReferenceV1.ParseExact(selected[..^1]+",\"Current\":true}"));
        var comparator=new InvestmentCaseComparatorOutputReferenceV1(InvestmentCaseComparatorOutputReferenceV1.ExpectedContractName,"1.0",Context,Comparator).ToExactJson();
        Assert.ThrowsAny<Exception>(()=>InvestmentCaseScenarioVersionReferenceV1.ParseExact(comparator));
    }

    [Fact]
    public void Exact_property_counts_and_no_copy_names_are_structural()
    {
        Assert.Equal(3,typeof(InvestmentCaseContextV1).GetProperties().Length);
        Assert.Equal(5,typeof(BudgetVersionReferenceV1).GetProperties().Length);
        Assert.Equal(5,typeof(ScenarioVersionReferenceV1).GetProperties().Length);
        Assert.Equal(5,typeof(ComparatorOutputReferenceV1).GetProperties().Length);
        foreach(var type in new[]{typeof(SelectedBudgetVersionReferenceV1),typeof(InvestmentCaseScenarioVersionReferenceV1),typeof(InvestmentCaseComparatorOutputReferenceV1),typeof(SelectedScenarioReferenceV1)})Assert.Equal(4,type.GetProperties().Length);
        var names=string.Join('|',new[]{typeof(SelectedBudgetVersionReferenceV1),typeof(InvestmentCaseScenarioVersionReferenceV1),typeof(InvestmentCaseComparatorOutputReferenceV1),typeof(SelectedScenarioReferenceV1)}.SelectMany(x=>x.GetProperties()).Select(x=>x.Name));
        foreach(var forbidden in new[]{"Amount","Currency","Period","Line","Assumption","Algorithm","Ranking","Metric","Payload","Occurrence","Disposition","Current","Selected"})Assert.DoesNotContain(forbidden,names,StringComparison.Ordinal);
    }

    private static void RoundTrip<T>(string json,Func<string,T> parse) where T:notnull=>Assert.Equal(json,(parse(json) switch{BudgetVersionReferenceV1 x=>x.ToExactJson(),ScenarioVersionReferenceV1 x=>x.ToExactJson(),ComparatorOutputReferenceV1 x=>x.ToExactJson(),SelectedBudgetVersionReferenceV1 x=>x.ToExactJson(),InvestmentCaseScenarioVersionReferenceV1 x=>x.ToExactJson(),InvestmentCaseComparatorOutputReferenceV1 x=>x.ToExactJson(),SelectedScenarioReferenceV1 x=>x.ToExactJson(),_=>throw new InvalidOperationException()}));
}
