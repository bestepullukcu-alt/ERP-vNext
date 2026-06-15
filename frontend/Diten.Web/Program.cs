using Diten.Web;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Web.Filters;
using Diten.Web.Services.Auth;
using Diten.Web.Services.EnterpriseStrategy;
using Diten.Web.Services.ManagementGovernance;
using Diten.Web.Services.WorkCenter;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
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
builder.Services.AddMemoryCache();
// FE-A-harden (A5): the default HttpClient is registered TRANSIENT — each scoped (per-request)
// controller resolves its own instance and makes only sequential calls, so a controller that mutates
// HttpClient.DefaultRequestHeaders.Authorization affects only its own request and cannot bleed a token
// across requests. Keep this registration transient; do NOT make HttpClient a singleton. New code
// should prefer the per-request HttpRequestMessage pattern (see GoldenReferenceSlimController /
// PlatformAuditController) so the guarantee holds even if this ever changes.
builder.Services.AddHttpClient();
builder.Services.AddScoped<Diten.Web.Services.IPlatformProfileSnapshotProvider, Diten.Web.Services.PlatformProfileSnapshotProvider>();
// FE-B (MOD-0018-FU9): UX-only permission snapshot for tenant RBAC screens. Not enforcement.
builder.Services.AddScoped<Diten.Web.Services.IPermissionSnapshot, Diten.Web.Services.PermissionSnapshot>();
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddSingleton<ITaskDetailService, TaskDetailService>();
builder.Services.AddScoped<IManagementGovernanceFrontendAdapter, MockManagementGovernanceFrontendAdapter>();
builder.Services.AddScoped<IEnterpriseStrategyFrontendAdapter, MockEnterpriseStrategyFrontendAdapter>();
// FE-A-harden (A4): trust forwarded scheme/host/ip from the TLS-terminating reverse proxy.
// KnownNetworks/Proxies cleared: the proxy topology is deployment-managed (containerized) and not a
// fixed IP, so the forwarded headers from the trusted edge are honoured.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "Diten.Web");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "Diten.Web", [
    new("JwtSettings:Secret", "Diten.Web", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "Diten.Web", SecretRequirementKind.JwtPreviousCollection, Required: false),
    new("ConnectionStrings:MongoDb", "Diten.Web", SecretRequirementKind.ConnectionString, Required: false)
]);

var app = builder.Build();

// FE-A-harden (A4): honour X-Forwarded-For / X-Forwarded-Proto from the TLS-terminating proxy so the
// request scheme (https) and client IP resolve correctly — required for Secure cookies to be emitted
// behind a reverse proxy. Must run before any middleware that reads the scheme / sets cookies.
app.UseForwardedHeaders();

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
    var cultureSet = IsReferenceDataTenantPath(context.Request.Path) ? supportedCultureSet : platformCultureSet;
    if (!isPlatformContext)
    {
        return Task.FromResult<ProviderCultureResult?>(null);
    }

    var requestedCulture = context.Request.Query["culture"].ToString();
    if (!string.IsNullOrWhiteSpace(requestedCulture))
    {
        var normalizedCulture = cultureSet.Contains(requestedCulture) ? requestedCulture : "en";
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(normalizedCulture, normalizedCulture));
    }

    var cultureCookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
    var cookieCulture = !string.IsNullOrWhiteSpace(cultureCookie)
        ? CookieRequestCultureProvider.ParseCookieValue(cultureCookie)?.Cultures.FirstOrDefault().Value
        : null;
    var resolvedCulture = !string.IsNullOrWhiteSpace(cookieCulture) && cultureSet.Contains(cookieCulture)
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
        var allowedSet = isPlatformContext && !IsReferenceDataTenantPath(context.Request.Path)
            ? platformCultureSet
            : supportedCultureSet;
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
    ClockSkew = TimeSpan.FromSeconds(30)
};

// FE-A-harden (A3): single-flight the eager refresh. Concurrent requests from the same browser carry
// the SAME (expired access, rotating refresh) cookies; without this, each would call refresh, the
// first rotates the refresh token, and the others fail with a now-invalid token and log the user out.
// Keyed by the old refresh token: all concurrent callers await ONE refresh task, then each writes the
// resulting new tokens to its own response. (Cookie writes stay per-request; only the gateway call is shared.)
var refreshInFlight = new ConcurrentDictionary<string, Lazy<Task<AuthBridgeResult>>>();

// MOD-0014: Validated Token-to-User State Bridge
app.Use(async (context, next) =>
{
    var accessToken = AuthTokenCookies.GetAccessToken(context.Request);
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
            var refreshToken = AuthTokenCookies.GetRefreshToken(context.Request);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                AuthTokenCookies.ClearTokens(context.Response);
            }
            else
            {
                var authGateway = context.RequestServices.GetRequiredService<IAuthGateway>();
                var authCookieService = context.RequestServices.GetRequiredService<IAuthCookieService>();
                var tenantId = TryReadTenantId(accessToken);
                try
                {
                    // Single-flight: one shared refresh call per old refresh token (CancellationToken.None
                    // so one request's cancellation does not abort the shared refresh); each caller then
                    // writes the resulting tokens to its own response below.
                    var refreshTask = refreshInFlight
                        .GetOrAdd(refreshToken, _ => new Lazy<Task<AuthBridgeResult>>(
                            () => authGateway.RefreshAsync(accessToken, refreshToken, tenantId, CancellationToken.None)))
                        .Value;

                    AuthBridgeResult refreshResult;
                    try
                    {
                        refreshResult = await refreshTask;
                    }
                    finally
                    {
                        refreshInFlight.TryRemove(refreshToken, out _);
                    }

                    if (!refreshResult.Success ||
                        string.IsNullOrWhiteSpace(refreshResult.AccessToken) ||
                        string.IsNullOrWhiteSpace(refreshResult.RefreshToken) ||
                        !refreshResult.ExpiresAt.HasValue)
                    {
                        if (refreshResult.ReauthRequired)
                        {
                            authCookieService.ClearTokens(context.Response);
                        }
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
                    // Soft failure: do not clear tokens on transient exceptions
                }
            }
        }
        catch
        {
            AuthTokenCookies.ClearTokens(context.Response);
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

// FE-A-core: friendly 401/403/404 pages, re-executed through HomeController.Status ([AllowAnonymous]).
// Only triggers for empty-body error responses (JSON API/proxy responses carry a body → unaffected).
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

// Keep JWT cookie based platform/tenant identity available after cookie auth runs.
app.Use(async (context, next) =>
{
    var accessToken = AuthTokenCookies.GetAccessToken(context.Request);
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
            AuthTokenCookies.ClearTokens(context.Response);
        }
    }

    await next();
});

app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (RequiresPlatformPasswordChange(context) && !IsPasswordChangeAllowedPath(context.Request.Path))
    {
        context.Response.Redirect("/platform/change-password");
        return;
    }

    await next();
});

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

static bool IsReferenceDataTenantPath(PathString path)
{
    return path.StartsWithSegments("/Platform/ReferenceData", StringComparison.OrdinalIgnoreCase);
}

static bool ShouldSkipTokenBridgeRefresh(PathString path)
{
    return path.StartsWithSegments("/account/refresh", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/logout", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/forgot-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/reset-password", StringComparison.OrdinalIgnoreCase);
}

static bool RequiresPlatformPasswordChange(HttpContext context)
{
    var actorType = context.User.FindFirst("actor_type")?.Value;
    var requiresChange = context.User.FindFirst("pwd_change_required")?.Value;
    return (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase)) &&
           string.Equals(requiresChange, "true", StringComparison.OrdinalIgnoreCase);
}

static bool IsPasswordChangeAllowedPath(PathString path)
{
    return path.StartsWithSegments("/platform/change-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/forgot-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/reset-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/logout", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase);
}
