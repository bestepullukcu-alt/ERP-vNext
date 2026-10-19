using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuRetirementRequestRecoveryContractTests
{
    [Fact]
    public void Worker_is_disabled_by_default_and_has_bounded_settings()
    {
        var options = new LskuRetirementRequestWorkflowWorkerOptions();
        Assert.False(options.Enabled);
        Assert.True(options.IsValid());
        Assert.False(new LskuRetirementRequestWorkflowWorkerOptions { BatchSize = 101 }.IsValid());
        Assert.False(new LskuRetirementRequestWorkflowWorkerOptions { LeaseSeconds = 9 }.IsValid());
    }

    [Fact]
    public void Command_line_accepts_only_one_exact_ordinal_argument()
    {
        Assert.True(LskuRetirementRequestRecoveryCommandLine.IsRequested(
            [LskuRetirementRequestRecoveryCommandLine.ExactArgument]));
        Assert.False(LskuRetirementRequestRecoveryCommandLine.IsRequested([]));
        Assert.Throws<InvalidOperationException>(() =>
            LskuRetirementRequestRecoveryCommandLine.IsRequested(["--RUN-LSKU-RETIREMENT-RECOVERY"]));
        Assert.Throws<InvalidOperationException>(() =>
            LskuRetirementRequestRecoveryCommandLine.IsRequested([
                LskuRetirementRequestRecoveryCommandLine.ExactArgument,
                LskuRetirementRequestRecoveryCommandLine.ExactArgument]));
    }

    [Fact]
    public void Disabled_workflow_configuration_cannot_be_materialized_as_enabled_runtime_facts()
    {
        var options = new LskuRetirementRequestWorkflowOptions();
        Assert.False(options.Enabled);
        Assert.Throws<InvalidOperationException>(() => options.ToConfiguration(Guid.NewGuid(), null));
    }
}
