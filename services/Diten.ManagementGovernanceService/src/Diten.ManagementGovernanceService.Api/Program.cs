using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Infrastructure;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;

namespace Diten.ManagementGovernanceService.Api;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (!args.Contains("--local-test", StringComparer.Ordinal)) return;
        var mongoUri=Environment.GetEnvironmentVariable("DWS_LOCAL_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(mongoUri)) throw new InvalidOperationException("dws_transaction_unavailable");
        var databaseName=Environment.GetEnvironmentVariable("DWS_LOCAL_TEST_DATABASE") ?? "diten_mg_dws_local_test";
        var app=BuildLocalTestApp(mongoUri,databaseName);
        await app.Services.GetRequiredService<DwsMongoIndexInitializer>().InitializeAsync();
        await app.RunAsync();
    }

    public static WebApplication BuildLocalTestApp(string mongoUri,string databaseName)
    {
        if(string.IsNullOrWhiteSpace(mongoUri)||string.IsNullOrWhiteSpace(databaseName))throw new InvalidOperationException("dws_transaction_unavailable");
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:5017");
        builder.Services.AddControllers().AddApplicationPart(typeof(Controllers.DwsStructuresController).Assembly);
        builder.Services.AddDwsApplication();
        builder.Services.AddDwsLocalTestInfrastructure(mongoUri,databaseName);
        var app=builder.Build();
        app.MapGet("/health",()=>Results.Ok(new { status="healthy", mode="local-test", module="MOD-0354" }));
        app.MapControllers();
        return app;
    }
}
