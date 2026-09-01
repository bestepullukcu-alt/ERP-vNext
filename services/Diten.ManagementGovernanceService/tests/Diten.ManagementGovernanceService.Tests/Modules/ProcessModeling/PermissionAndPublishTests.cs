using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling;

public sealed class PermissionAndPublishTests
{
    [Fact] public void Permission_and_command_sets_are_exact()
    {
        Assert.Equal("MOD-0355", ProcessModelingPermissions.ModuleCode);
        Assert.Equal(16, ProcessModelingPermissions.ExactPermissions.Count);
        Assert.Equal(16, ProcessModelingPermissions.ExactPermissions.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(20, ProcessModelingPermissions.ExactCommandMap.Count);
        Assert.Equal("management-governance.process-modeling.models.publish", ProcessModelingPermissions.ExactCommandMap["PublishProcessModelVersionCommand"]);
    }

    [Fact] public void Publish_application_execution_is_fail_closed_until_second_slice()
    {
        var result = PublishProcessModelVersionContract.FailClosed();
        Assert.False(result.Accepted); Assert.Equal(503, result.HttpStatus); Assert.Equal("process_model_publish_second_slice_unavailable", result.StableCode);
    }

    [Theory]
    [InlineData(false,true,true,true,true,true,400)] [InlineData(true,false,true,true,true,true,401)] [InlineData(true,true,false,true,true,true,403)] [InlineData(true,true,true,false,true,true,404)] [InlineData(true,true,true,true,false,true,409)] [InlineData(true,true,true,true,true,false,503)]
    public void Core_boundary_has_exact_failure_matrix(bool valid,bool authenticated,bool permitted,bool visible,bool current,bool available,int status)=>Assert.Equal(status,ProcessModelingCoreBoundary.Evaluate(valid,authenticated,permitted,visible,current,available).HttpStatus);

    [Fact] public void Query_permission_map_is_exact()=>Assert.Equal(new[]{"management-governance.process-modeling.architectures.read","management-governance.process-modeling.definitions.read","management-governance.process-modeling.models.read"},ProcessModelingPermissions.ExactQueryMap.Values);
}
