using System.Text;
using System.Text.Json;
using Diten.FpaService.Application.Modules.Budgeting.Contracts;
using Diten.FpaService.Domain.Modules.Budgeting.Financial;
using Diten.FpaService.Domain.Modules.Budgeting.Selections;
using Diten.FpaService.Domain.Modules.Budgeting.Versions;

namespace Diten.FpaService.ContractTests.Modules.Budgeting;

public sealed class FinancialAndBilateralContractTests
{
    [Theory]
    [InlineData(1,"FY2027","Original","", "0.0000","2262b02bc94923998e72e797f4e3996246e38b69aaa34349b119a111b919be13")]
    public void Canonical_financial_vector_one_is_byte_exact(long number,string label,string baseline,string notes,string amount,string expectedHash)
    {
        var bytes=Canonical(number,label,Enum.Parse<FundingBaselineType>(baseline),notes,[new("FY2027-P01","Revenue",LineType.Base,new(decimal.Parse(amount,System.Globalization.CultureInfo.InvariantCulture)))]);
        Assert.Equal(AuthorityVector(number),bytes);
        Assert.Equal(expectedHash,BudgetContentCanonicalizer.Hash(bytes));
        Assert.DoesNotContain("\r",Encoding.UTF8.GetString(bytes));
        Assert.False(bytes.AsSpan().EndsWith("\n"u8));
    }

    [Fact] public void Canonical_financial_vector_two_is_byte_exact()
    {
        var bytes=Canonical(2,"Réforecast",FundingBaselineType.Reforecast,"Board case",
        [new("FY2027-P01","Revenue",LineType.Base,new(1000m)),new("FY2027-P01","Revenue",LineType.Adjustment,new(-25.125m)),new("FY2027-P02","Credit",LineType.Credit,new(-10m))]);
        Assert.Equal(AuthorityVector(2),bytes);
        Assert.Equal("2e0c437ca89d23d2602f8b2320d158e59d924284761ed6d8d4529a05a319fceb",BudgetContentCanonicalizer.Hash(bytes));
    }

    [Fact] public void Ppm_context_and_wrapper_are_exact_compact_fixtures()
    {
        var context=new InvestmentCaseContextV1("ppm.investment-case-context","1.0",Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var reference=new BudgetVersionReferenceV1("fpa.budget-version-reference","1.0",Guid.Parse("22222222-2222-2222-2222-222222222222"),Guid.Parse("33333333-3333-3333-3333-333333333333"),7);
        Assert.Equal("{\"ContractName\":\"ppm.investment-case-context\",\"ContractVersion\":\"1.0\",\"InvestmentCaseId\":\"11111111-1111-1111-1111-111111111111\"}",JsonSerializer.Serialize(context));
        var wrapper=new SelectedBudgetVersionReferenceV1(SelectedBudgetVersionReferenceV1.ExpectedContractName,"1.0",context,reference);
        Assert.Equal("{\"ContractName\":\"ppm.investment-case-selected-budget-version-reference\",\"ContractVersion\":\"1.0\",\"InvestmentCaseContext\":{\"ContractName\":\"ppm.investment-case-context\",\"ContractVersion\":\"1.0\",\"InvestmentCaseId\":\"11111111-1111-1111-1111-111111111111\"},\"BudgetVersionReference\":{\"ContractName\":\"fpa.budget-version-reference\",\"ContractVersion\":\"1.0\",\"BudgetId\":\"22222222-2222-2222-2222-222222222222\",\"BudgetVersionId\":\"33333333-3333-3333-3333-333333333333\",\"BudgetVersionNumber\":7}}",JsonSerializer.Serialize(wrapper));
    }

    [Fact] public void Permission_manifest_is_exact_closed_fifteen()
    {
        Assert.Equal(15,BudgetingPermissions.ExactManifest.Count);
        Assert.All(BudgetingPermissions.ExactManifest,x=>Assert.Equal(x.Operation,x.Permission));
        Assert.Equal("budgeting.budgets.read",BudgetingPermissions.ExactManifest[0].Operation);
        Assert.Equal("budgeting.budget-version-references.validate",BudgetingPermissions.ExactManifest[^1].Operation);
    }

    private static byte[] Canonical(long number,string label,FundingBaselineType baseline,string notes,IReadOnlyList<BudgetVersionDraftLine> draftLines)
    {
        var calendarId=Guid.Parse("22222222-2222-2222-2222-222222222222");
        var start=new DateOnly(2026,7,1);
        var periods=Enumerable.Range(1,12).Select(i=>new BudgetPeriod(Guid.NewGuid(),Guid.Empty,$"FY2027-P{i:00}",i,start.AddMonths(i-1),start.AddMonths(i))).ToArray();
        var lines=draftLines.Select(x=>new BudgetLine(Guid.Empty,x.PeriodReference,x.LineKey,x.LineType,x.Amount)).ToArray();
        return BudgetContentCanonicalizer.Write(Guid.Parse("11111111-1111-1111-1111-111111111111"),number,label,new("fpa.currency-reference","1.0","USD"),new("fpa.budget-fiscal-calendar-reference","1.0",calendarId),UnitSemantics.Units,baseline,start,notes,periods,lines);
    }
    private static byte[] AuthorityVector(long number){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"AGENTS.md")))d=d.Parent;var pack=Path.Combine(d!.FullName,"execution","domains","enterprise-strategy-business-performance","module-packs","MOD-0136-budgeting.md");var marker=$"\"BudgetVersionNumber\":{number},";return Encoding.UTF8.GetBytes(File.ReadLines(pack).Single(x=>x.StartsWith("{\"ContractName\":\"fpa.budget-content-canonical-json.v1\"",StringComparison.Ordinal)&&x.Contains(marker,StringComparison.Ordinal)));}
}
