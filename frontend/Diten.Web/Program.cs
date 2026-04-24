using Diten.Web;
using Diten.Web.Filters;
using Diten.Web.Services.Auth;
using Diten.Web.Services.EnterpriseStrategy;
using Diten.Web.Services.ManagementGovernance;
using Diten.Web.Services.WorkCenter;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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
        options.ViewLocationFormats.Add("/Views/Platform/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/Archive/{1}/{0}.cshtml");
    });

builder.Services.Configure<MvcOptions>(options =>
{
    options.Filters.Add<ShellAccessFilter>();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "auth_ticket"; // Use a separate cookie for ASP.NET state if needed
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
    });

var authServiceUrl = builder.Configuration["AuthServiceUrl"] ?? "http://localhost:5056";
builder.Services.AddHttpClient<IAuthGateway, AuthGateway>(client =>
{
    client.BaseAddress = new Uri(authServiceUrl);
});
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddSingleton<ITaskDetailService, TaskDetailService>();
builder.Services.AddScoped<IManagementGovernanceFrontendAdapter, MockManagementGovernanceFrontendAdapter>();
builder.Services.AddScoped<IEnterpriseStrategyFrontendAdapter, MockEnterpriseStrategyFrontendAdapter>();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data", "uploads"));

var supportedCultures = new[] { "en", "fr", "es", "zh", "ar", "ru", "tr" };
var supportedCultureSet = new HashSet<string>(supportedCultures, StringComparer.OrdinalIgnoreCase);
var platformSupportedCultures = new[] { "en", "tr" };
var platformCultureSet = new HashSet<string>(platformSupportedCultures, StringComparer.OrdinalIgnoreCase);
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
        var requestedCulture = culture.ToString();
        var requestHost = context.Request.Host.Host ?? string.Empty;
        var isPlatformContext = requestHost.StartsWith("admin.", StringComparison.OrdinalIgnoreCase) ||
                                context.Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase);
        var allowedSet = isPlatformContext ? platformCultureSet : supportedCultureSet;
        var normalizedCulture = allowedSet.Contains(requestedCulture) ? requestedCulture : "en";
        var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture));
        context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, cookieValue, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
    }
    await next();
});

var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? string.Empty;
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? string.Empty;
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? string.Empty;
var validatedTokenParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtIssuer,
    ValidAudience = jwtAudience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
    ClockSkew = TimeSpan.Zero
};

// MOD-0014: Validated Token-to-User State Bridge
app.Use(async (context, next) =>
{
    var accessToken = context.Request.Cookies["access_token"];
    if (!string.IsNullOrEmpty(accessToken))
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(accessToken, validatedTokenParameters, out _);
            context.User = principal;
        }
        catch
        {
            context.Response.Cookies.Delete("access_token");
            context.Response.Cookies.Delete("refresh_token");
        }
    }
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async context =>
    {
        var host = context.Request.Host.Host;
        var isAdminHost = host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
        context.Response.Redirect(isAdminHost ? "/Platform/Tenants" : "/Skus");
    });
});

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Skus}/{action=Index}/{id?}");

app.Run();
