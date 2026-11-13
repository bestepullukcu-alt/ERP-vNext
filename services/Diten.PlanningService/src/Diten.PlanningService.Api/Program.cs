using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Api.ModuleRegistration;
using Diten.PlanningService.Infrastructure.Features.DemandPlanning;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5068");

builder.Services.AddControllers();
builder.Services.AddDemandPlanningFeature(builder.Configuration);
builder.Services.AddDemandRevisionEventTransport(builder.Configuration, builder.Environment);
builder.Services.Configure<PlatformRegistrationOptions>(
    builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IModuleManifestProvider, DemandPlanningManifestProvider>();
builder.Services.AddSingleton<IModuleManifestProvider, MrpManifestProvider>();
builder.Services.AddHostedService<ModuleRegistrationHostedService>();

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (!PlanningCorrelation.IsValid(context))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseMiddleware<DemandTenantMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapDemandPlanningHealth();
app.Run();

public partial class Program;
