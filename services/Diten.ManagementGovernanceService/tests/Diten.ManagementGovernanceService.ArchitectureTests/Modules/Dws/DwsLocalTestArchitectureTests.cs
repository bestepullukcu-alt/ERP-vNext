using Diten.ManagementGovernanceService.Api;
using Diten.ManagementGovernanceService.Api.Controllers;
using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Application.Behaviors;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsLocalTestArchitectureTests
{
    [Fact]
    public async Task Program_without_local_test_switch_exits_without_host()
    {
        var execution=Program.Main([]);
        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(execution.IsCompletedSuccessfully);
    }

    [Fact]
    public void Application_composition_has_exact_four_pipeline_behaviors()
    {
        var services=new ServiceCollection();
        services.AddLogging();
        services.AddDwsApplication();
        var behaviors=services.Where(x=>x.ServiceType.IsGenericType&&x.ServiceType.GetGenericTypeDefinition()==typeof(IPipelineBehavior<,>)).Select(x=>x.ImplementationType).ToArray();
        Assert.Equal(new[]{typeof(ValidationBehavior<,>),typeof(LoggingBehavior<,>),typeof(ExceptionBehavior<,>),typeof(PerformanceBehavior<,>)},behaviors);
    }

    [Fact]
    public void Production_infrastructure_composition_does_not_activate_local_executor()
    {
        var services=new ServiceCollection();
        services.AddDwsInfrastructure();
        Assert.DoesNotContain(services,x=>x.ServiceType==typeof(IDwsLocalActionExecutor));
    }

    [Fact]
    public void Controller_exposes_exact_fifteen_cqrs_routes()
    {
        var methods=typeof(DwsStructuresController).GetMethods().Where(method=>method.GetCustomAttributes(false).OfType<HttpMethodAttribute>().Any()).ToArray();
        Assert.Equal(15,methods.Length);
        Assert.Equal(10,methods.Count(method=>method.GetCustomAttributes(false).OfType<HttpMethodAttribute>().Single().HttpMethods.Single() is "POST" or "PUT" or "DELETE"));
        Assert.Equal(5,methods.Count(method=>method.GetCustomAttributes(false).OfType<HttpMethodAttribute>().Single().HttpMethods.Single()=="GET"));
    }

    [Fact]
    public async Task Local_test_host_is_bound_to_exact_loopback_5017_configuration()
    {
        var app=Program.BuildLocalTestApp("mongodb://127.0.0.1:65535","configuration_only");
        try{Assert.Equal("http://127.0.0.1:5017",app.Configuration[WebHostDefaults.ServerUrlsKey]);}
        finally{await app.DisposeAsync();}
    }
}
