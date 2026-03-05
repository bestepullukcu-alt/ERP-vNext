using Diten.Web;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options => {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource));
    })
    .AddRazorOptions(options =>
    {
        options.ViewLocationFormats.Add("/Views/MDM/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/Archive/{1}/{0}.cshtml");
    });
builder.Services.AddHttpClient();

var app = builder.Build();

var supportedCultures = new[] { "en", "tr", "es", "ru", "uz", "uk", "ka", "kk" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Persist culture in session/cookie for subsequent requests (e.g., POST)
app.Use(async (context, next) =>
{
    var culture = context.Request.Query["culture"];
    if (!string.IsNullOrEmpty(culture))
    {
        var cookieValue = Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(culture));
        context.Response.Cookies.Append(Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName, cookieValue, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
    }
    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async context =>
    {
        context.Response.Redirect("/LegalEntities");
    });
});
app.MapControllers();

app.Run();
