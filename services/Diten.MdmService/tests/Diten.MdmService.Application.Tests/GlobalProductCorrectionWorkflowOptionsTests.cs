using Diten.MdmService.Api.Configuration;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductCorrectionWorkflowOptionsTests
{
    [Fact]
    public void Defaults_are_disabled_and_worker_bounds_are_fail_closed()
    {
        Assert.False(new GlobalProductCorrectionWorkflowOptions().Enabled);
        Assert.False(new GlobalProductCorrectionWorkflowWorkerOptions { BatchSize = 101 }.IsValid());
        Assert.True(new GlobalProductCorrectionWorkflowWorkerOptions().IsValid());
    }
}
