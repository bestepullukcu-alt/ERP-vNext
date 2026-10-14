using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Api.ModuleRegistration;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5068");

builder.Services.AddControllers();
builder.Services.AddDemandPlanningFeature(builder.Configuration);
builder.Services.Configure<PlatformRegistrationOptions>(
    builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IModuleManifestProvider, DemandPlanningManifestProvider>();
builder.Services.AddHostedService<ModuleRegistrationHostedService>();

var app = builder.Build();
app.UseAuthentication();
app.UseMiddleware<DemandTenantMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapDemandPlanningHealth();
app.Run();

public partial class Program;
