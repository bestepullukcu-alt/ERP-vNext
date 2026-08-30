using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Api.LocalTest;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Infrastructure;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Microsoft.AspNetCore.Authentication;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.PerformanceImprovement;
using Diten.ManagementGovernanceService.Api.Authentication;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingUserAcceptance;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Api;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (!args.Contains("--local-test", StringComparer.Ordinal)) return;
        var mongoUri=Environment.GetEnvironmentVariable("DWS_LOCAL_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(mongoUri)) throw new InvalidOperationException("dws_transaction_unavailable");
        var databaseName=Environment.GetEnvironmentVariable("DWS_LOCAL_TEST_DATABASE") ?? "diten_mg_dws_local_test";
        var userAcceptance=args.Contains("--user-acceptance-runtime",StringComparer.Ordinal);
        var app=userAcceptance?BuildUserAcceptanceApp(mongoUri,databaseName):BuildLocalTestApp(mongoUri,databaseName);
        await app.Services.GetRequiredService<DwsMongoIndexInitializer>().InitializeAsync();
        await app.Services.GetRequiredService<PerformanceImprovementMongoPersistence.IndexInitializer>().InitializeAsync();
        if(userAcceptance)await app.Services.GetRequiredService<ProcessModelingMongoIndexInitializer>().InitializeAsync();
        await app.RunAsync();
    }

    public static WebApplication BuildLocalTestApp(string mongoUri,string databaseName)
    {
        if(string.IsNullOrWhiteSpace(mongoUri)||string.IsNullOrWhiteSpace(databaseName))throw new InvalidOperationException("dws_transaction_unavailable");
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:5017");
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(Controllers.DwsStructuresController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
                options.JsonSerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = _ => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                    Diten.ManagementGovernanceService.Application.Features.Dws.Response<object>.Fail(
                        DwsErrors.InvalidRequest,
                        StatusCodes.Status400BadRequest));
            });
        builder.Services.AddSingleton<DwsLocalTestIdentityFixture>();
        builder.Services.AddSingleton<IDwsLocalTestTrustedContextResolver, DwsLocalTestTrustedContextResolver>();
        builder.Services
            .AddAuthentication(DwsLocalTestAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, DwsLocalTestAuthenticationHandler>(
                DwsLocalTestAuthenticationDefaults.Scheme,
                _ => { });
        builder.Services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions,LocalTestAuthenticationHandler>(
                LocalTestAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddDwsApplication();
        builder.Services.AddDwsLocalTestInfrastructure(mongoUri,databaseName);
        builder.Services.AddSingleton<IProcessModelingLocalCommandExecutor,DefaultUnavailableProcessModelingLocalCommandExecutor>();
        var app=builder.Build();
        app.UseMiddleware<DwsLocalTestExceptionBoundary>();
        app.MapGet("/health",()=>Results.Ok(new { status="healthy", mode="local-test", module="MOD-0354" }));
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<DwsLocalTestJsonBoundary>();
        app.MapControllers();
        return app;
    }

    public static WebApplication BuildUserAcceptanceApp(string mongoUri,string databaseName,Action<IConfigurationBuilder>? configure=null)
    {
        if(string.IsNullOrWhiteSpace(mongoUri)||string.IsNullOrWhiteSpace(databaseName))throw new InvalidOperationException("process_model_provider_unavailable");
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:5017");
        configure?.Invoke(builder.Configuration);
        builder.Services.AddControllers(options=>options.Conventions.Add(new DisableLocalImprovementControllerConvention()))
            .AddApplicationPart(typeof(Controllers.ProcessModelingController).Assembly)
            .AddJsonOptions(options=>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive=false;
                options.JsonSerializerOptions.UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
            });
        builder.Services.AddUserAcceptanceJwt(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddDwsApplication();
        builder.Services.AddDwsLocalTestInfrastructure(mongoUri,databaseName);
        var client=new MongoClient(mongoUri);
        builder.Services.AddSingleton(new ProcessModelingMongoContext(client,databaseName));
        builder.Services.AddSingleton<ProcessModelingMongoIndexInitializer>();
        builder.Services.AddSingleton<IProcessModelUserAcceptanceQueries,ProcessModelingUserAcceptanceQueryStore>();
        // Core mutations remain fail-closed until their complete Mongo executor is composition-ready.
        builder.Services.AddSingleton<IProcessModelingLocalCommandExecutor,DefaultUnavailableProcessModelingLocalCommandExecutor>();
        var app=builder.Build();
        app.UseMiddleware<DwsLocalTestExceptionBoundary>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<DwsLocalTestJsonBoundary>();
        app.MapControllers();
        return app;
    }

    private sealed class DisableLocalImprovementControllerConvention:Microsoft.AspNetCore.Mvc.ApplicationModels.IControllerModelConvention
    {
        public void Apply(Microsoft.AspNetCore.Mvc.ApplicationModels.ControllerModel controller)
        {
            if(controller.ControllerType==typeof(Controllers.PerformanceImprovementLocalTestController))controller.Actions.Clear();
        }
    }
}
