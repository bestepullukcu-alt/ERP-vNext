using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.Dws;

public sealed class DwsLocalTestContractTests
{
    [Fact]
    public void Self_registration_is_exact_and_contains_only_six_permissions()
    {
        var contract=DwsSelfRegistration.Contract;
        Assert.Equal("MOD-0354",contract.ModuleCode);
        Assert.Equal("tenant",contract.Shell);
        Assert.Equal("/management-governance/delivery-execution/structures",contract.RoutePath);
        Assert.Equal(6,contract.Permissions.Count);
        Assert.Equal(DwsAuthorizationManifest.Entries.Select(x=>x.Permission).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),contract.Permissions);
    }

    [Fact]
    public async Task Dispatch_rejects_operation_contract_drift_before_executor()
    {
        var executor=new ProbeExecutor();
        var handler=new DwsDispatchHandler(executor);
        var request=new DwsDispatchRequest("GetStructureTreeQuery",new GetStructureByIdQuery(Guid.NewGuid()),new(Guid.NewGuid(),Guid.NewGuid(),"key"));
        var error=await Assert.ThrowsAsync<DwsValidationException>(()=>handler.Handle(request,CancellationToken.None));
        Assert.Equal(DwsErrors.InvalidRequest,error.Code);
        Assert.Equal(0,executor.Calls);
    }

    [Fact]
    public async Task Dispatch_returns_standard_response_envelope()
    {
        var executor=new ProbeExecutor();
        var handler=new DwsDispatchHandler(executor);
        var contract=new GetStructureByIdQuery(Guid.NewGuid());
        var response=await handler.Handle(new(contract.GetType().Name,contract,new(Guid.NewGuid(),Guid.NewGuid(),"key")),CancellationToken.None);
        Assert.True(response.IsSuccessful);
        Assert.Equal(200,response.StatusCode);
        Assert.Equal(1,executor.Calls);
    }

    private sealed class ProbeExecutor:IDwsLocalActionExecutor
    {
        public int Calls{get;private set;}
        public Task<DwsLocalResult> ExecuteAsync(DwsDispatchRequest request,CancellationToken cancellationToken){Calls++;return Task.FromResult(new DwsLocalResult(request.Operation,"validated","test"));}
    }
}
