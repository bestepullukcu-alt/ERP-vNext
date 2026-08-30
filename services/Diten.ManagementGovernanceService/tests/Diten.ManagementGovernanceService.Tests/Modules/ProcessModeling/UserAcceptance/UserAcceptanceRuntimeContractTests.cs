using Diten.ManagementGovernanceService.Api.Authentication;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.UserAcceptance;

public sealed class UserAcceptanceRuntimeContractTests
{
    [Fact]
    public void Jwt_composition_fails_closed_without_required_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddUserAcceptanceJwt(configuration));
        Assert.Contains("JwtSettings:Issuer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_and_improvement_permissions_remain_disjoint_exact_families()
    {
        Assert.Equal(16, ProcessModelingPermissions.ExactPermissions.Count);
        var improvement = new[]
        {
            PerformanceImprovementPermissions.ImprovementRead,
            PerformanceImprovementPermissions.ImprovementCreate,
            PerformanceImprovementPermissions.ImprovementUpdate,
            PerformanceImprovementPermissions.ImprovementQualify,
            PerformanceImprovementPermissions.ImprovementRecordOutcome,
            PerformanceImprovementPermissions.ImprovementClose
        };
        Assert.Equal(6, improvement.Distinct(StringComparer.Ordinal).Count());
        Assert.All(improvement, permission => Assert.DoesNotContain(permission, ProcessModelingPermissions.ExactPermissions));
    }
}
