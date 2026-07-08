using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class GatewayWorkflowStartClientTests
{
    [Fact]
    public void Infrastructure_DoesNotRegisterWorkflowStartClient_ForCurrentP2Scope()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowStartClient));
    }
}
