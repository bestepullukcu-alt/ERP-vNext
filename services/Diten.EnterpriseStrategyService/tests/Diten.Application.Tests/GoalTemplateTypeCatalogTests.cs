using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Application.EnterpriseStrategy.Validators;

namespace Diten.Application.Tests;

public sealed class GoalTemplateTypeCatalogTests
{
    [Theory]
    [InlineData("Profitability", "Financial")]
    [InlineData("Market Position", "Market")]
    [InlineData("Risk/Compliance", "Risk")]
    [InlineData("Portfolio", "Financial")]
    [InlineData("Cash/Liquidity", "Financial")]
    [InlineData("Supply Chain", "Operations")]
    [InlineData("Regulatory", "Risk")]
    [InlineData("Partner Effectiveness", "Market")]
    [InlineData("Pharmacovigilance", "Pharmacovigilance")]
    public void Normalize_Maps_Legacy_Goal_Template_Types(string source, string expected)
    {
        var actual = GoalTemplateTypeCatalog.Normalize(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateGoal_Accepts_New_Canonical_Type_List()
    {
        var errors = EnterpriseStrategyValidators.ValidateGoal(new GoalDto
        {
            GoalTitle = "Patient safety objective",
            GoalStatement = "Improve pharmacovigilance controls",
            Category = "Pharmacovigilance",
            StrategicThemeId = "theme-pv",
            Status = "Draft",
            Priority = "High",
            OwnerRole = "usr-safety",
            OwnerCompanyId = "cmp-001",
            StrategyPeriodId = "sp-001",
            ApplicabilityMode = "Enterprise"
        });

        Assert.DoesNotContain("categoryCode", errors.Keys, StringComparer.OrdinalIgnoreCase);
    }
}
