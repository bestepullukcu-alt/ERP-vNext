using Diten.PlanningService.Api.Features.DemandPlanning;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5068");

builder.Services.AddControllers();
builder.Services.AddDemandPlanningFeature(builder.Configuration);

var app = builder.Build();
app.UseAuthentication();
app.UseMiddleware<DemandTenantMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "live", module = "MOD-0188" }))
    .AllowAnonymous();
app.Run();

public partial class Program;