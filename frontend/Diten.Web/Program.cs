using Diten.Web;
using Diten.BuildingBlocks.Security.Secrets;
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
using Microsoft.Extensions.DependencyInjection;

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

var authServiceUrl = builder.Configuration["GatewayUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient<IAuthGateway, AuthGateway>(client =>
{
    client.BaseAddress = new Uri(authServiceUrl);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddSingleton<ITaskDetailService, TaskDetailService>();
builder.Services.AddScoped<IManagementGovernanceFrontendAdapter, MockManagementGovernanceFrontendAdapter>();
builder.Services.AddScoped<IEnterpriseStrategyFrontendAdapter, MockEnterpriseStrategyFrontendAdapter>();
builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "Diten.Web");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "Diten.Web", [
    new("JwtSettings:Secret", "Diten.Web", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "Diten.Web", SecretRequirementKind.JwtPreviousCollection, Required: false),
    new("ConnectionStrings:MongoDb", "Diten.Web", SecretRequirementKind.ConnectionString, Required: false)
]);

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
localizationOptions.RequestCultureProviders.Insert(0, new CustomRequestCultureProvider(context =>
{
    var requestHost = context.Request.Host.Host ?? string.Empty;
    var isPlatformContext = requestHost.StartsWith("admin.", StringComparison.OrdinalIgnoreCase) ||
                            context.Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase);
    if (!isPlatformContext)
    {
        return Task.FromResult<ProviderCultureResult?>(null);
    }

    var requestedCulture = context.Request.Query["culture"].ToString();
    if (!string.IsNullOrWhiteSpace(requestedCulture))
    {
        var normalizedCulture = platformCultureSet.Contains(requestedCulture) ? requestedCulture : "en";
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(normalizedCulture, normalizedCulture));
    }

    var cultureCookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
    var cookieCulture = !string.IsNullOrWhiteSpace(cultureCookie)
        ? CookieRequestCultureProvider.ParseCookieValue(cultureCookie)?.Cultures.FirstOrDefault().Value
        : null;
    var resolvedCulture = !string.IsNullOrWhiteSpace(cookieCulture) && platformCultureSet.Contains(cookieCulture)
        ? cookieCulture
        : "en";

    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(resolvedCulture, resolvedCulture));
}));

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
var jwtRotationResolver = new JwtSecretRotationResolver(builder.Configuration);
var validatedTokenParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtIssuer,
    ValidAudience = jwtAudience,
    IssuerSigningKeys = jwtRotationResolver.GetValidationKeys(),
    ClockSkew = TimeSpan.Zero
};

// MOD-0014: Validated Token-to-User State Bridge
app.Use(async (context, next) =>
{
    var accessToken = context.Request.Cookies["access_token"];
    if (!string.IsNullOrEmpty(accessToken) && !ShouldSkipTokenBridgeRefresh(context.Request.Path))
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(accessToken, validatedTokenParameters, out _);
            context.User = principal;
        }
        catch (SecurityTokenExpiredException)
        {
            var refreshToken = context.Request.Cookies["refresh_token"];
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                context.Response.Cookies.Delete("access_token");
                context.Response.Cookies.Delete("refresh_token");
            }
            else
            {
                var authGateway = context.RequestServices.GetRequiredService<IAuthGateway>();
                var authCookieService = context.RequestServices.GetRequiredService<IAuthCookieService>();
                var tenantId = TryReadTenantId(accessToken);
                try
                {
                    var refreshResult = await authGateway.RefreshAsync(accessToken, refreshToken, tenantId, context.RequestAborted);

                    if (!refreshResult.Success ||
                        string.IsNullOrWhiteSpace(refreshResult.AccessToken) ||
                        string.IsNullOrWhiteSpace(refreshResult.RefreshToken) ||
                        !refreshResult.ExpiresAt.HasValue)
                    {
                        authCookieService.ClearTokens(context.Response);
                    }
                    else
                    {
                        authCookieService.WriteTokens(
                            context.Response,
                            refreshResult.AccessToken,
                            refreshResult.RefreshToken,
                            refreshResult.ExpiresAt.Value);

                        var principal = handler.ValidateToken(refreshResult.AccessToken, validatedTokenParameters, out _);
                        context.User = principal;
                    }
                }
                catch
                {
                    authCookieService.ClearTokens(context.Response);
                }
            }
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

// Keep JWT cookie based platform/tenant identity available after cookie auth runs.
app.Use(async (context, next) =>
{
    var accessToken = context.Request.Cookies["access_token"];
    if (!string.IsNullOrEmpty(accessToken) && !ShouldSkipTokenBridgeRefresh(context.Request.Path))
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
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

app.UseAuthorization();

app.MapGet("/", (HttpContext context) =>
{
    var host = context.Request.Host.Host;
    var isAdminHost = host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
    return Results.Redirect(isAdminHost ? "/Platform/Tenants" : "/WorkCenter");
});

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=WorkCenter}/{action=Index}/{id?}");

app.Run();

static Guid? TryReadTenantId(string accessToken)
{
    try
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            return null;
        }

        var token = handler.ReadJwtToken(accessToken);
        var claimValue = token.Claims.FirstOrDefault(c =>
            string.Equals(c.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Type, "tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : null;
    }
    catch
    {
        return null;
    }
}

static bool ShouldSkipTokenBridgeRefresh(PathString path)
{
    return path.StartsWithSegments("/account/refresh", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/logout", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/login", StringComparison.OrdinalIgnoreCase);
}
