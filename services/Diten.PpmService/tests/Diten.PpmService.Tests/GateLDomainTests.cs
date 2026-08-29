using Diten.PpmService.Domain.Entities;
using Xunit;
using Diten.PpmService.Application.Common;
using System.Reflection;

namespace Diten.PpmService.Tests;

public sealed class GateLDomainTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();

    [Fact]
    public void Investment_case_has_immutable_parent_and_exact_terminal_lifecycle()
    {
        var parent = Guid.NewGuid();
        var entity = new InvestmentCase(_tenant, _actor, " IC-1 ", " Case ", null, parent,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        Assert.Equal(parent, entity.PortfolioId);
        Assert.Equal("IC-1", entity.Code);
        entity.Transition(_actor, InvestmentCaseLifecycleState.UnderAnalysis);
        entity.Transition(_actor, InvestmentCaseLifecycleState.Closed);
        Assert.False(entity.IsReferenceable);
        Assert.False(entity.CanTransitionTo(InvestmentCaseLifecycleState.Withdrawn));
        Assert.DoesNotContain(typeof(InvestmentCase).GetMethods(), m => m.Name == "Update" &&
            m.GetParameters().Any(p => p.Name == "portfolioId"));
    }

    [Fact]
    public void Investment_case_rejects_reversed_planned_dates()
    {
        Assert.Throws<ArgumentException>(() => new InvestmentCase(_tenant, _actor, "IC", "Case", null,
            Guid.NewGuid(), new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void Benefit_has_only_investment_case_parent_and_terminal_guards()
    {
        var parent = Guid.NewGuid();
        var entity = new BenefitCommitment(_tenant, _actor, "BC-1", "Benefit", null, parent,
            "Reduce processing time", null);
        Assert.Equal(parent, entity.InvestmentCaseId);
        Assert.Null(typeof(BenefitCommitment).GetProperty("PortfolioId"));
        entity.Transition(_actor, BenefitCommitmentLifecycleState.Planned);
        entity.Transition(_actor, BenefitCommitmentLifecycleState.Active);
        entity.Transition(_actor, BenefitCommitmentLifecycleState.Closed);
        Assert.False(entity.CanTransitionTo(BenefitCommitmentLifecycleState.Cancelled));
        Assert.Throws<InvalidOperationException>(() => entity.Transition(_actor, BenefitCommitmentLifecycleState.Cancelled));
    }

    [Fact]
    public void Benefit_contains_no_actual_realization_or_approval_fields()
    {
        var forbidden = new[] { "PortfolioId", "ActualValue", "RealizedValue", "Evidence", "OutcomeId", "ApprovedAt", "ApprovedBy" };
        var names = typeof(BenefitCommitment).GetProperties().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(forbidden, name => Assert.DoesNotContain(name, names));
    }

    [Fact]
    public void Gate_l_permission_subset_is_exact_and_has_no_delete_or_alias()
    {
        var actual = typeof(PpmPermissions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.Name.StartsWith("InvestmentCases", StringComparison.Ordinal) || x.Name.StartsWith("BenefitCommitments", StringComparison.Ordinal))
            .Select(x => (string)x.GetRawConstantValue()!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var expected = new[]
        {
            "ppm.benefit-commitments.change-lifecycle", "ppm.benefit-commitments.create",
            "ppm.benefit-commitments.read", "ppm.benefit-commitments.update",
            "ppm.investment-cases.change-lifecycle", "ppm.investment-cases.create",
            "ppm.investment-cases.read", "ppm.investment-cases.update"
        };
        Assert.Equal(expected, actual);
        Assert.DoesNotContain(actual, x => x.Contains("delete", StringComparison.Ordinal) || x.Contains('*'));
    }
}
