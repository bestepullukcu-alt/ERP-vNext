using Diten.WebUI.Services.EnterpriseStrategy;
using Diten.WebUI.Services.ManagementGovernance;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IManagementGovernanceFrontendAdapter, MockManagementGovernanceFrontendAdapter>();
builder.Services.AddScoped<IEnterpriseStrategyFrontendAdapter, MockEnterpriseStrategyFrontendAdapter>();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data", "uploads"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
