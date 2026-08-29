using System.Text;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.LocalTest;

public sealed class LocalTestContractTests
{
    [Fact]
    public void Normative_request_hash_is_exact()
    {
        var hash=ProcessModelingLocalRequestHash.Compute(
            "POST","/internal/local-test/v1/process-modeling/architectures","CreateProcessArchitectureCommand",
            Guid.Parse("11111111-1111-4111-8111-111111111111"),Guid.Parse("22222222-2222-4222-8222-222222222222"),
            "management-governance.process-modeling.architectures.create",
            Encoding.UTF8.GetBytes("{\"architectureCode\":\"EA\",\"name\":\"Enterprise Architecture\",\"sortOrder\":1}"));
        Assert.Equal("6bba6bff9291532f124c3b5087521b7e3ceb3c3725467236fe22f1e0c7f428f4",hash);
    }

    [Fact]
    public void Exact_twenty_routes_use_exact_command_permission_manifest()
    {
        Assert.Equal(20,ProcessModelingLocalRoutes.Exact.Count);
        Assert.Equal(20,ProcessModelingLocalRoutes.Exact.Select(x=>x.Command).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ProcessModelingLocalRoutes.Exact,x=>Assert.Equal(ProcessModelingPermissions.ExactCommandMap[x.Command],x.Permission));
        Assert.DoesNotContain(ProcessModelingLocalRoutes.Exact,x=>x.Template.Contains("//",StringComparison.Ordinal));
    }
}
